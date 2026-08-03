using System.IO;
using Microsoft.Win32;

namespace MgaRegistryChecker.Services;

/// <summary>
/// ログオン時スタートアップ（HKCU Run）への登録。
/// 登録時は <c>--check</c> 付きで起動し、差分チェックのみ行って終了する。
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Run キーに書き込む値の名前（アプリ表示名）。</summary>
    public const string ValueName = AppPaths.AppDisplayName;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException(UiText.ErrStartupRegistryUnavailable);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = ResolveExecutablePath();
        key.SetValue(ValueName, $"\"{exePath}\" {AppLaunchArgs.CheckArgument}");
    }

    private static string ResolveExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return path;

        path = Environment.GetCommandLineArgs().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(path.Trim('"'));
            if (File.Exists(path))
                return path;
        }

        throw new InvalidOperationException(UiText.ErrStartupExePathUnknown);
    }
}
