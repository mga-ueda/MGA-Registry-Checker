using System.Windows;
using MGA_RegistryChecker.Models;

namespace MGA_RegistryChecker.Services;

/// <summary>監視場所の差分比較と、差分ダイアログ結果の適用。</summary>
public sealed class WatchDiffProcessor
{
    private readonly RegistrySnapshotService _registry;
    private readonly SnapshotStore _store;

    public WatchDiffProcessor(RegistrySnapshotService registry, SnapshotStore store)
    {
        _registry = registry;
        _store = store;
    }

    public sealed class ProcessResult
    {
        public bool AnyDifferences { get; init; }
        public bool HadErrors { get; init; }
    }

    /// <param name="silent">true のとき、差分なし・監視なしでは UI / ステータス更新をしない。</param>
    public ProcessResult Process(
        AppState state,
        IReadOnlyList<WatchedLocation> locations,
        Window? owner,
        Action<string>? setStatus = null,
        bool silent = false)
    {
        if (locations.Count == 0)
        {
            if (!silent)
                setStatus?.Invoke(UiText.StatusNoWatches);
            return new ProcessResult();
        }

        var anyDiff = false;
        var hadErrors = false;

        foreach (var loc in locations)
        {
            LocationDiff diff;
            try
            {
                if (!silent)
                    setStatus?.Invoke(UiText.StatusChecking(loc.Path));
                diff = _registry.Compare(loc);
            }
            catch (Exception ex)
            {
                hadErrors = true;
                AppDialog.Error(owner, UiText.MsgCompareFailed(loc.Path, ex.Message));
                continue;
            }

            if (diff.Changes.Count == 0)
                continue;

            anyDiff = true;
            var dlg = new DiffWindow(diff);
            if (owner is not null)
                dlg.Owner = owner;
            else
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dlg.ShowDialog();
            ApplyDecision(state, loc, diff, dlg, setStatus, silent);
        }

        if (!anyDiff && !silent)
            setStatus?.Invoke(UiText.StatusNoDifferences);

        return new ProcessResult
        {
            AnyDifferences = anyDiff,
            HadErrors = hadErrors
        };
    }

    private void ApplyDecision(
        AppState state,
        WatchedLocation loc,
        LocationDiff diff,
        DiffWindow dlg,
        Action<string>? setStatus,
        bool silent)
    {
        switch (dlg.Decision)
        {
            case DiffWindow.DiffDecision.Accept:
                loc.Keys = diff.CurrentSnapshot;
                loc.CapturedAt = DateTime.Now;
                _store.Save(state);
                if (!silent)
                    setStatus?.Invoke(UiText.StatusAccepted(loc.Path));
                break;
            case DiffWindow.DiffDecision.Revert:
                try
                {
                    loc.Keys = _registry.Capture(loc);
                    loc.CapturedAt = DateTime.Now;
                    _store.Save(state);
                    if (!silent)
                        setStatus?.Invoke(UiText.StatusReverted(loc.Path));
                }
                catch (Exception ex)
                {
                    AppDialog.Warning(dlg, UiText.MsgRecaptureAfterRevertFailed(ex.Message));
                }
                break;
            case DiffWindow.DiffDecision.Mixed:
                try
                {
                    var accepted = dlg.ItemResults
                        .Where(x => x.Action == DiffWindow.ItemAction.Accept)
                        .Select(x => x.Change)
                        .ToList();
                    RegistrySnapshotService.AcceptChangesIntoSnapshot(loc, diff, accepted);
                    _store.Save(state);
                    if (!silent)
                    {
                        setStatus?.Invoke(UiText.StatusMixedApplied(
                            loc.Path,
                            accepted.Count,
                            dlg.ItemResults.Count(x => x.Action == DiffWindow.ItemAction.Revert)));
                    }
                }
                catch (Exception ex)
                {
                    AppDialog.Warning(dlg, UiText.MsgMixedApplyFailed(ex.Message));
                }
                break;
            default:
                if (!silent)
                    setStatus?.Invoke(UiText.StatusSkipped(loc.Path));
                break;
        }
    }
}
