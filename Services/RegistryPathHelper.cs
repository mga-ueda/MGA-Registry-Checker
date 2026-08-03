using System.IO;
using Microsoft.Win32;

namespace MGA_RegistryChecker.Services;

public static class RegistryPathHelper
{
    private static readonly (string Alias, RegistryHive Hive, string Name)[] Hives =
    [
        ("HKEY_CLASSES_ROOT", RegistryHive.ClassesRoot, "HKEY_CLASSES_ROOT"),
        ("HKCR", RegistryHive.ClassesRoot, "HKEY_CLASSES_ROOT"),
        ("HKEY_CURRENT_USER", RegistryHive.CurrentUser, "HKEY_CURRENT_USER"),
        ("HKCU", RegistryHive.CurrentUser, "HKEY_CURRENT_USER"),
        ("HKEY_LOCAL_MACHINE", RegistryHive.LocalMachine, "HKEY_LOCAL_MACHINE"),
        ("HKLM", RegistryHive.LocalMachine, "HKEY_LOCAL_MACHINE"),
        ("HKEY_USERS", RegistryHive.Users, "HKEY_USERS"),
        ("HKU", RegistryHive.Users, "HKEY_USERS"),
        ("HKEY_CURRENT_CONFIG", RegistryHive.CurrentConfig, "HKEY_CURRENT_CONFIG"),
        ("HKCC", RegistryHive.CurrentConfig, "HKEY_CURRENT_CONFIG"),
    ];

    public static IReadOnlyList<(string Name, RegistryHive Hive)> RootHives { get; } =
    [
        ("HKEY_CLASSES_ROOT", RegistryHive.ClassesRoot),
        ("HKEY_CURRENT_USER", RegistryHive.CurrentUser),
        ("HKEY_LOCAL_MACHINE", RegistryHive.LocalMachine),
        ("HKEY_USERS", RegistryHive.Users),
        ("HKEY_CURRENT_CONFIG", RegistryHive.CurrentConfig),
    ];

    public static bool TryParse(string path, out RegistryHive hive, out string subKey)
    {
        hive = default;
        subKey = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        path = path.Trim().TrimEnd('\\');
        foreach (var (alias, hiveValue, _) in Hives.OrderByDescending(h => h.Alias.Length))
        {
            if (path.Equals(alias, StringComparison.OrdinalIgnoreCase))
            {
                hive = hiveValue;
                subKey = string.Empty;
                return true;
            }

            if (path.StartsWith(alias + "\\", StringComparison.OrdinalIgnoreCase))
            {
                hive = hiveValue;
                subKey = path[(alias.Length + 1)..];
                return true;
            }
        }

        return false;
    }

    public static string Normalize(string path)
    {
        if (!TryParse(path, out var hive, out var subKey))
            return path.Trim().TrimEnd('\\');

        var root = RootHives.First(h => h.Hive == hive).Name;
        return string.IsNullOrEmpty(subKey) ? root : $"{root}\\{subKey}";
    }

    public static RegistryKey? OpenKey(string path, bool writable = false)
    {
        if (!TryParse(path, out var hive, out var subKey))
            return null;

        var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        if (string.IsNullOrEmpty(subKey))
            return baseKey;

        var key = baseKey.OpenSubKey(subKey, writable);
        if (key is null)
            baseKey.Dispose();
        else
            baseKey.Dispose();

        return key;
    }

    public static string Combine(string parent, string child) =>
        string.IsNullOrEmpty(parent) ? child : $"{parent}\\{child}";

    public static PathValidationResult Validate(string path, string? valueName)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathValidationResult.Ng(UiText.ValidateKeyEmpty);

        if (!TryParse(path, out _, out _))
            return PathValidationResult.Ng(UiText.ValidateKeyFormat);

        var normalized = Normalize(path);
        using var key = OpenKey(normalized);
        if (key is null)
            return PathValidationResult.Ng(UiText.ValidateKeyMissing);

        if (valueName is null)
            return PathValidationResult.Ok(UiText.ValidateKeyOkAllValues(normalized));

        try
        {
            _ = key.GetValueKind(valueName);
            return PathValidationResult.Ok(
                UiText.ValidateKeyOkSingleValue(normalized, UiText.DisplayValueLabel(valueName)));
        }
        catch (IOException)
        {
            return PathValidationResult.Ng(UiText.ValidateValueMissing(UiText.DisplayValueLabel(valueName)));
        }
    }
}

public readonly record struct PathValidationResult(bool IsOk, string Message)
{
    public static PathValidationResult Ok(string message) => new(true, message);
    public static PathValidationResult Ng(string message) => new(false, message);
}
