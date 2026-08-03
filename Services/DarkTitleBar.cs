using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MgaRegistryChecker.Services;

/// <summary>Windows のタイトルバーをアプリのダークテーマに合わせる。</summary>
public static partial class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    // タイトルバーはパネル色 #242830（本文背景 #1A1D23 と差をつける）/ 前景 #E8EAED
    // COLORREF = 0x00BBGGRR
    private const uint CaptionColorRef = 0x00302824;
    private const uint BorderColorRef = 0x004C403A;
    private const uint TextColorRef = 0x00EDEAE8;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        void ApplyNow()
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            SetImmersiveDarkMode(hwnd, enabled: true);
            SetColorAttribute(hwnd, DwmwaCaptionColor, CaptionColorRef);
            SetColorAttribute(hwnd, DwmwaBorderColor, BorderColorRef);
            SetColorAttribute(hwnd, DwmwaTextColor, TextColorRef);
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            ApplyNow();
        else
            window.SourceInitialized += (_, _) => ApplyNow();
    }

    private static void SetImmersiveDarkMode(IntPtr hwnd, bool enabled)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;

        var attribute = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985)
            ? DwmwaUseImmersiveDarkMode
            : DwmwaUseImmersiveDarkModeBefore20H1;

        var useDark = enabled ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, attribute, ref useDark, sizeof(int));
    }

    private static void SetColorAttribute(IntPtr hwnd, int attribute, uint colorRef)
    {
        // キャプション色などは Windows 11 以降
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        var color = unchecked((int)colorRef);
        _ = DwmSetWindowAttribute(hwnd, attribute, ref color, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
