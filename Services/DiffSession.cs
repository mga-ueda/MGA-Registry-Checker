using System.Windows;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

/// <summary>監視場所の差分比較と、差分ダイアログ結果の適用オーケストレーション。</summary>
public sealed class DiffSession(
    DiffApplyService apply,
    IDiffPresenter presenter)
{
    private readonly DiffApplyService _apply = apply;
    private readonly IDiffPresenter _presenter = presenter;

    public sealed class ProcessResult
    {
        public bool AnyDifferences { get; init; }
        public bool HadErrors { get; init; }
    }

    /// <param name="silent">true のとき、差分なし・監視なしでは UI / ステータス更新をしない。</param>
    public ProcessResult Process(
        AppState state,
        IReadOnlyList<WatchedLocation> locations,
        Window? ownerWindow,
        Action<string>? setStatus = null,
        bool silent = false)
    {
        if (locations.Count == 0)
        {
            if (!silent)
                setStatus?.Invoke(UiText.StatusNoWatches);
            return new ProcessResult();
        }

        var diffs = new List<LocationDiff>();
        var hadErrors = false;

        foreach (var loc in locations)
        {
            try
            {
                if (!silent)
                    setStatus?.Invoke(UiText.StatusChecking(loc.Path));
                var diff = RegistrySnapshotService.Compare(loc);
                if (diff.Changes.Count > 0)
                    diffs.Add(diff);
            }
            catch (Exception ex)
            {
                hadErrors = true;
                AppDialog.Error(ownerWindow, UiText.MsgCompareFailed(loc.Path, ex.Message));
            }
        }

        if (diffs.Count == 0)
        {
            if (!silent && !hadErrors)
                setStatus?.Invoke(UiText.StatusNoDifferences);
            return new ProcessResult { HadErrors = hadErrors };
        }

        var result = _presenter.Show(
            diffs,
            ownerWindow,
            tryCommit: dialogResult => TryCommit(state, diffs, dialogResult, ownerWindow));

        UpdateStatusAfterDecision(result, diffs, setStatus, silent);

        return new ProcessResult
        {
            AnyDifferences = true,
            HadErrors = hadErrors
        };
    }

    private bool TryCommit(
        AppState state,
        IReadOnlyList<LocationDiff> diffs,
        DiffDialogResult result,
        Window? owner)
    {
        if (result.Decision == DiffDecision.Cancel)
            return true;

        try
        {
            foreach (var diff in diffs)
            {
                var sliced = DiffApplyService.SliceForLocation(result, diff);
                if (sliced.Items.Count == 0 || sliced.Decision == DiffDecision.Cancel)
                    continue;
                DiffApplyService.ApplyRegistryWrites(diff, sliced);
            }
        }
        catch (Exception ex)
        {
            AppDialog.Error(owner, UiText.MsgRestoreFailed(ex.Message), UiText.TitleRestoreError);
            return false;
        }

        try
        {
            foreach (var diff in diffs)
            {
                var sliced = DiffApplyService.SliceForLocation(result, diff);
                if (sliced.Items.Count == 0 || sliced.Decision == DiffDecision.Cancel)
                    continue;
                DiffApplyService.ApplySnapshotUpdate(diff, sliced);
            }

            _apply.Save(state);
        }
        catch (Exception ex)
        {
            if (result.Decision == DiffDecision.Revert)
            {
                AppDialog.Warning(owner, UiText.MsgRecaptureAfterRevertFailed(ex.Message));
                return true;
            }

            if (result.Decision == DiffDecision.Mixed)
            {
                AppDialog.Warning(owner, UiText.MsgMixedApplyFailed(ex.Message));
                return true;
            }

            AppDialog.Error(owner, UiText.MsgRestoreFailed(ex.Message), UiText.TitleRestoreError);
            return false;
        }

        return true;
    }

    private static void UpdateStatusAfterDecision(
        DiffDialogResult result,
        List<LocationDiff> diffs,
        Action<string>? setStatus,
        bool silent)
    {
        if (silent || setStatus is null)
            return;

        var watchCount = diffs.Count;
        var changeCount = diffs.Sum(d => d.Changes.Count);
        var singlePath = watchCount == 1
            ? UiText.FormatWatchPath(diffs[0].Location)
            : null;

        switch (result.Decision)
        {
            case DiffDecision.Accept:
                setStatus(singlePath is not null
                    ? UiText.StatusAccepted(singlePath)
                    : UiText.StatusAcceptedMulti(watchCount, changeCount));
                break;
            case DiffDecision.Revert:
                setStatus(singlePath is not null
                    ? UiText.StatusReverted(singlePath)
                    : UiText.StatusRevertedMulti(watchCount, changeCount));
                break;
            case DiffDecision.Mixed:
                setStatus(singlePath is not null
                    ? UiText.StatusMixedApplied(
                        singlePath,
                        result.Items.Count(x => x.Action == DiffItemAction.Accept),
                        result.Items.Count(x => x.Action == DiffItemAction.Revert))
                    : UiText.StatusMixedAppliedMulti(
                        watchCount,
                        result.Items.Count(x => x.Action == DiffItemAction.Accept),
                        result.Items.Count(x => x.Action == DiffItemAction.Revert)));
                break;
            default:
                setStatus(singlePath is not null
                    ? UiText.StatusSkipped(singlePath)
                    : UiText.StatusSkippedMulti(watchCount, changeCount));
                break;
        }
    }
}
