using MGA_RegistryChecker.Models;

namespace MGA_RegistryChecker.Services;

/// <summary>差分ダイアログの表示抽象（Services は WPF を直接参照しない）。</summary>
public interface IDiffPresenter
{
    /// <summary>
    /// 差分を表示し、ユーザー決定を返す。
    /// <paramref name="tryCommit"/> が指定された場合、APPLY 時に閉じる前に呼び、false ならダイアログを開いたままにする。
    /// </summary>
    DiffDialogResult Show(
        LocationDiff diff,
        object? ownerWindow,
        Func<DiffDialogResult, bool>? tryCommit = null,
        bool simulateOnly = false);
}
