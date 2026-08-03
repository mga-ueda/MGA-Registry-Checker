using System.IO;
using System.Media;
using Microsoft.Win32;
using System.Windows;

namespace MGA_RegistryChecker.Services;

/// <summary>
/// コントロールパネルのサウンド設定（プログラム イベント）に従って効果音を鳴らす MessageBox。
/// </summary>
public static class AppDialog
{
    private static SoundPlayer? _activePlayer;

    public static MessageBoxResult Show(
        Window owner,
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        PlaySound(icon);
        return MessageBox.Show(owner, message, title, buttons, icon);
    }

    public static void Info(Window owner, string message, string? title = null) =>
        Show(owner, message, title ?? UiText.TitleInfo, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Warning(Window owner, string message, string? title = null) =>
        Show(owner, message, title ?? UiText.TitleWarning, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Error(Window owner, string message, string? title = null) =>
        Show(owner, message, title ?? UiText.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);

    public static MessageBoxResult Confirm(Window owner, string message, string? title = null) =>
        Show(owner, message, title ?? UiText.TitleConfirm, MessageBoxButton.YesNo, MessageBoxImage.Question);

    private static void PlaySound(MessageBoxImage icon)
    {
        // Error==Hand, Warning==Exclamation, Information==Asterisk（同一値）
        // コントロールパネル「プログラム イベント」と同じキーを参照する
        var eventNames = icon switch
        {
            MessageBoxImage.Error => new[] { "SystemHand", "SystemNotification", ".Default" },
            MessageBoxImage.Warning => new[] { "SystemExclamation", "SystemNotification", ".Default" },
            MessageBoxImage.Question => new[] { "SystemQuestion", "SystemNotification", "SystemAsterisk", ".Default" },
            MessageBoxImage.Information => new[] { "SystemNotification", "SystemAsterisk", ".Default" },
            _ => new[] { "SystemNotification", ".Default" }
        };

        foreach (var name in eventNames)
        {
            if (TryPlaySchemeSound(name))
                return;
        }
    }

    private static bool TryPlaySchemeSound(string eventName)
    {
        try
        {
            var path = ReadSchemeWavPath(eventName);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            var player = new SoundPlayer(path);
            player.Load();
            _activePlayer = player;
            player.Play();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// HKCU\AppEvents\Schemes\Apps\.Default\{event}\.Current の割り当て WAV を読む。
    /// </summary>
    private static string? ReadSchemeWavPath(string eventName)
    {
        var keyPath = $@"AppEvents\Schemes\Apps\.Default\{eventName}\.Current";
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        var value = key?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // 環境変数や相対表記を解決
        value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        return value;
    }
}
