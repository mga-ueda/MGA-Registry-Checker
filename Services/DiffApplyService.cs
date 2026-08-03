using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

/// <summary>
/// 差分ダイアログ結果をレジストリ書込とスナップショット更新に反映する（副作用の単一入口）。
/// </summary>
public sealed class DiffApplyService(SnapshotStore store)
{
    private readonly SnapshotStore _store = store;

    /// <summary>複数監視分の差分結果を一括適用し、最後に 1 回だけ保存する。</summary>
    public void ApplyBatch(AppState state, IReadOnlyList<LocationDiff> diffs, DiffDialogResult result)
    {
        if (result.Decision == DiffDecision.Cancel || diffs.Count == 0)
            return;

        foreach (var diff in diffs)
        {
            var sliced = SliceForLocation(result, diff);
            if (sliced.Items.Count == 0 || sliced.Decision == DiffDecision.Cancel)
                continue;
            ApplyRegistryWrites(diff, sliced);
        }

        foreach (var diff in diffs)
        {
            var sliced = SliceForLocation(result, diff);
            if (sliced.Items.Count == 0 || sliced.Decision == DiffDecision.Cancel)
                continue;
            ApplySnapshotUpdate(state, diff, sliced, save: false);
        }

        _store.Save(state);
    }

    public void Save(AppState state) => _store.Save(state);

    /// <summary>REVERT / Mixed のレジストリ書き戻し。失敗時は例外。</summary>
    public static void ApplyRegistryWrites(LocationDiff diff, DiffDialogResult result)
    {
        if (result.Decision == DiffDecision.Revert)
        {
            RegistrySnapshotService.Revert(diff.Location);
            return;
        }

        if (result.Decision != DiffDecision.Mixed)
            return;

        var toRevert = result.Items
            .Where(x => x.Action == DiffItemAction.Revert)
            .Select(x => x.Change)
            .ToList();
        if (toRevert.Count > 0)
            RegistrySnapshotService.RevertChanges(diff.Location, toRevert);
    }

    /// <summary>スナップショット更新。失敗時は例外。</summary>
    public void ApplySnapshotUpdate(
        AppState state,
        LocationDiff diff,
        DiffDialogResult result,
        bool save = true)
    {
        var loc = diff.Location;

        switch (result.Decision)
        {
            case DiffDecision.Accept:
                loc.Keys = diff.CurrentSnapshot;
                loc.CapturedAt = DateTime.Now;
                break;
            case DiffDecision.Revert:
                loc.Keys = RegistrySnapshotService.Capture(loc);
                loc.CapturedAt = DateTime.Now;
                break;
            case DiffDecision.Mixed:
            {
                var accepted = result.Items
                    .Where(x => x.Action == DiffItemAction.Accept)
                    .Select(x => x.Change)
                    .ToList();
                RegistrySnapshotService.AcceptChangesIntoSnapshot(loc, diff, accepted);
                break;
            }
            default:
                return;
        }

        if (save)
            _store.Save(state);
    }

    public static DiffDialogResult SliceForLocation(DiffDialogResult overall, LocationDiff diff)
    {
        var items = overall.Items
            .Where(x => x.LocationId == diff.Location.Id)
            .ToList();
        if (items.Count == 0)
            return new DiffDialogResult { Decision = DiffDecision.Cancel };

        return new DiffDialogResult
        {
            Decision = DecisionFromItems(items),
            Items = items
        };
    }

    public static DiffDecision DecisionFromItems(IReadOnlyList<DiffItemChoice> items)
    {
        if (items.Count == 0)
            return DiffDecision.Cancel;

        var distinct = items.Select(i => i.Action).Distinct().ToList();
        if (distinct.Count != 1)
            return DiffDecision.Mixed;

        return distinct[0] switch
        {
            DiffItemAction.Accept => DiffDecision.Accept,
            DiffItemAction.Revert => DiffDecision.Revert,
            _ => DiffDecision.Cancel
        };
    }
}
