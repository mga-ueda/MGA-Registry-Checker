using System.Windows;

namespace MGA_RegistryChecker.Services;

/// <summary>ウィンドウ位置の復元・プライマリ画面中央寄せ。</summary>
public static class WindowPlacement
{
    private const double MinVisiblePx = 64;

    /// <summary>プライマリディスプレイの作業領域の中央に配置する（位置の記憶はしない）。</summary>
    public static void CenterOnPrimary(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var work = SystemParameters.WorkArea;
        var width = window.Width;
        var height = window.Height;
        if (double.IsNaN(width) || width <= 0)
            width = window.ActualWidth;
        if (double.IsNaN(height) || height <= 0)
            height = window.ActualHeight;

        window.Left = work.Left + Math.Max(0, (work.Width - width) / 2);
        window.Top = work.Top + Math.Max(0, (work.Height - height) / 2);
    }

    public static void Apply(Window window, Models.WindowBounds? bounds)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (bounds is null || !IsUsable(bounds))
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        if (bounds.IsMaximized)
            window.WindowState = WindowState.Maximized;
    }

    public static Models.WindowBounds Capture(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var restored = window.RestoreBounds;
        var useRestored = window.WindowState != WindowState.Normal
                          && restored.Width > 0
                          && restored.Height > 0;

        return new Models.WindowBounds
        {
            Left = useRestored ? restored.Left : window.Left,
            Top = useRestored ? restored.Top : window.Top,
            Width = useRestored ? restored.Width : window.Width,
            Height = useRestored ? restored.Height : window.Height,
            IsMaximized = window.WindowState == WindowState.Maximized
        };
    }

    private static bool IsUsable(Models.WindowBounds bounds)
    {
        if (bounds.Width < 200 || bounds.Height < 150)
            return false;
        if (double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top)
            || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
            return false;

        // 仮想デスクトップ上に最低限見えていれば有効（ディスプレイ構成変更にも耐える）
        var vsLeft = SystemParameters.VirtualScreenLeft;
        var vsTop = SystemParameters.VirtualScreenTop;
        var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
        var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

        var right = bounds.Left + bounds.Width;
        var bottom = bounds.Top + bounds.Height;

        var visibleWidth = Math.Min(right, vsRight) - Math.Max(bounds.Left, vsLeft);
        var visibleHeight = Math.Min(bottom, vsBottom) - Math.Max(bounds.Top, vsTop);
        return visibleWidth >= MinVisiblePx && visibleHeight >= MinVisiblePx;
    }
}
