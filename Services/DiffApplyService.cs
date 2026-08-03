using MGA_RegistryChecker.Models;

namespace MGA_RegistryChecker.Services;

/// <summary>
/// 差分ダイアログ結果をレジストリ書込とスナップショット更新に反映する（副作用の単一入口）。
/// </summary>
public sealed class DiffApplyService
{
    private readonly RegistrySnapshotService _registry;
    private readonly SnapshotStore _store;

    public DiffApplyService(RegistrySnapshotService registry, SnapshotStore store)
    {
        _registry = registry;
        _store = store;
    }

    /// <summary>REVERT / Mixed のレジストリ書き戻し。失敗時は例外。</summary>
    public void ApplyRegistryWrites(LocationDiff diff, DiffDialogResult result)
    {
        if (result.Decision == DiffDecision.Revert)
        {
            _registry.Revert(diff.Location);
            return;
        }

        if (result.Decision != DiffDecision.Mixed)
            return;

        var toRevert = result.Items
            .Where(x => x.Action == DiffItemAction.Revert)
            .Select(x => x.Change)
            .ToList();
        if (toRevert.Count > 0)
            _registry.RevertChanges(diff.Location, toRevert);
    }

    /// <summary>スナップショット更新と保存。失敗時は例外。</summary>
    public void ApplySnapshotUpdate(AppState state, LocationDiff diff, DiffDialogResult result)
    {
        var loc = diff.Location;

        switch (result.Decision)
        {
            case DiffDecision.Accept:
                loc.Keys = diff.CurrentSnapshot;
                loc.CapturedAt = DateTime.Now;
                break;
            case DiffDecision.Revert:
                loc.Keys = _registry.Capture(loc);
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

        _store.Save(state);
    }
}
