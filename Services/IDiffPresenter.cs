using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

/// <summary>差分ダイアログの表示抽象（Services は WPF を直接参照しない）。</summary>
public interface IDiffPresenter
{
    /// <summary>
    /// 1 つ以上の監視場所の差分をまとめて表示し、ユーザー決定を返す。
    /// <paramref name="tryCommit"/> が指定された場合、APPLY 時に閉じる前に呼び、false ならダイアログを開いたままにする。
    /// </summary>
    DiffDialogResult Show(
        IReadOnlyList<LocationDiff> diffs,
        object? ownerWindow,
        Func<DiffDialogResult, bool>? tryCommit = null,
        bool simulateOnly = false);
}
