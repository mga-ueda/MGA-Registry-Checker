using System.Windows;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

/// <summary>監視場所の差分比較と、差分ダイアログ結果の適用オーケストレーション。</summary>
public sealed class DiffSession
{
    private readonly RegistrySnapshotService _registry;
    private readonly DiffApplyService _apply;
    private readonly IDiffPresenter _presenter;

    public DiffSession(
        RegistrySnapshotService registry,
        DiffApplyService apply,
        IDiffPresenter presenter)
    {
        _registry = registry;
        _apply = apply;
        _presenter = presenter;
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
                AppDialog.Error(ownerWindow, UiText.MsgCompareFailed(loc.Path, ex.Message));
                continue;
            }

            if (diff.Changes.Count == 0)
                continue;

            anyDiff = true;
            var result = _presenter.Show(
                diff,
                ownerWindow,
                tryCommit: dialogResult => TryCommit(state, diff, dialogResult, ownerWindow));

            UpdateStatusAfterDecision(result, loc.Path, setStatus, silent);
        }

        if (!anyDiff && !silent)
            setStatus?.Invoke(UiText.StatusNoDifferences);

        return new ProcessResult
        {
            AnyDifferences = anyDiff,
            HadErrors = hadErrors
        };
    }

    private bool TryCommit(AppState state, LocationDiff diff, DiffDialogResult result, Window? owner)
    {
        try
        {
            _apply.ApplyRegistryWrites(diff, result);
        }
        catch (Exception ex)
        {
            AppDialog.Error(owner, UiText.MsgRestoreFailed(ex.Message), UiText.TitleRestoreError);
            return false;
        }

        try
        {
            _apply.ApplySnapshotUpdate(state, diff, result);
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
        string path,
        Action<string>? setStatus,
        bool silent)
    {
        if (silent || setStatus is null)
            return;

        switch (result.Decision)
        {
            case DiffDecision.Accept:
                setStatus(UiText.StatusAccepted(path));
                break;
            case DiffDecision.Revert:
                setStatus(UiText.StatusReverted(path));
                break;
            case DiffDecision.Mixed:
                setStatus(UiText.StatusMixedApplied(
                    path,
                    result.Items.Count(x => x.Action == DiffItemAction.Accept),
                    result.Items.Count(x => x.Action == DiffItemAction.Revert)));
                break;
            default:
                setStatus(UiText.StatusSkipped(path));
                break;
        }
    }
}
