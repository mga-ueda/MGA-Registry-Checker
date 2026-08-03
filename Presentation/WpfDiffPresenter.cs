using System.Windows;
using MgaRegistryChecker.Models;
using MgaRegistryChecker.Services;

namespace MgaRegistryChecker.Presentation;

public sealed class WpfDiffPresenter : IDiffPresenter
{
    public DiffDialogResult Show(
        IReadOnlyList<LocationDiff> diffs,
        object? ownerWindow,
        Func<DiffDialogResult, bool>? tryCommit = null,
        bool simulateOnly = false)
    {
        var dlg = new DiffWindow(diffs, tryCommit, simulateOnly);
        // Owner は前面表示のためだけ。位置は DiffWindow 側でプライマリ中央に固定する。
        if (ownerWindow is Window owner)
            dlg.Owner = owner;

        dlg.ShowDialog();
        return dlg.Result;
    }
}
