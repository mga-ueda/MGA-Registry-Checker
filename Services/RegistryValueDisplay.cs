using Microsoft.Win32;

namespace MGA_RegistryChecker.Services;

/// <summary>差分ダイアログ等向けの値表示整形。</summary>
public static class RegistryValueDisplay
{
    public static string Format(string? data, RegistryValueKind kind)
    {
        if (data is null)
            return UiText.ValueNull;

        return kind switch
        {
            RegistryValueKind.Binary or RegistryValueKind.None =>
                data.Length == 0
                    ? UiText.ValueEmpty
                    : UiText.ValueBinarySummary(Convert.FromBase64String(data).Length, Truncate(data, 40)),
            RegistryValueKind.MultiString => data.Replace('\n', '|'),
            RegistryValueKind.DWord => $"0x{uint.Parse(data):X8} ({data})",
            RegistryValueKind.QWord => $"0x{ulong.Parse(data):X16} ({data})",
            _ => Truncate(data, 120)
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
