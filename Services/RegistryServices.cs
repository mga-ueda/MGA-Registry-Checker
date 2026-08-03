using System.IO;
using System.Text;
using Microsoft.Win32;
using MGA_RegistryChecker.Models;

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
            // Keep baseKey alive via returned key's ownership — dispose base when done with key.
            // Caller owns the returned key; we dispose baseKey only if open failed.
            // Actually OpenSubKey doesn't transfer base ownership. Dispose base after we're done.
            // Better pattern: return key and let caller dispose; dispose baseKey now if we got a subkey.
            // RegistryKey from OpenSubKey doesn't need base kept open on modern .NET.
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

public static class RegistryValueCodec
{
    public static string? Encode(object? value, RegistryValueKind kind)
    {
        if (value is null)
            return null;

        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => value.ToString(),
            RegistryValueKind.MultiString => string.Join("\n", (string[])value),
            RegistryValueKind.DWord => Convert.ToUInt32(value).ToString(),
            RegistryValueKind.QWord => Convert.ToUInt64(value).ToString(),
            RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
            RegistryValueKind.None => value is byte[] noneBytes ? Convert.ToBase64String(noneBytes) : value.ToString(),
            _ => value is byte[] bytes ? Convert.ToBase64String(bytes) : value.ToString()
        };
    }

    public static object? Decode(string? data, RegistryValueKind kind)
    {
        if (data is null)
            return kind == RegistryValueKind.MultiString ? Array.Empty<string>() : null;

        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => data,
            RegistryValueKind.MultiString => data.Length == 0 ? Array.Empty<string>() : data.Split('\n'),
            RegistryValueKind.DWord => uint.Parse(data),
            RegistryValueKind.QWord => ulong.Parse(data),
            RegistryValueKind.Binary or RegistryValueKind.None =>
                data.Length == 0 ? Array.Empty<byte>() : Convert.FromBase64String(data),
            _ => Convert.FromBase64String(data)
        };
    }

    public static string FormatForDisplay(string? data, RegistryValueKind kind)
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

public sealed class RegistrySnapshotService
{
    public List<RegistryKeySnapshot> Capture(WatchedLocation location) =>
        Capture(location.Path, location.Mode, location.ValueName);

    public List<RegistryKeySnapshot> Capture(string path, WatchMode mode, string? valueName = null)
    {
        path = RegistryPathHelper.Normalize(path);
        var result = new List<RegistryKeySnapshot>();

        using var key = RegistryPathHelper.OpenKey(path);
        if (key is null)
            throw new InvalidOperationException($"Could not open registry key: {path}");

        if (mode == WatchMode.SingleValue)
        {
            CaptureSingleValue(path, key, valueName ?? string.Empty, result);
            return result;
        }

        CaptureKey(path, key, mode, result);
        return result;
    }

    private static void CaptureSingleValue(
        string fullPath, RegistryKey key, string valueName, List<RegistryKeySnapshot> result)
    {
        var snapshot = new RegistryKeySnapshot { Path = fullPath };
        try
        {
            var kind = key.GetValueKind(valueName);
            var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            snapshot.Values.Add(new RegistryValueData
            {
                Name = valueName,
                Kind = kind,
                Data = RegistryValueCodec.Encode(value, kind)
            });
        }
        catch (IOException)
        {
            // 値が無い場合は空スナップショット（削除検知用）
        }

        result.Add(snapshot);
    }

    private static void CaptureKey(string fullPath, RegistryKey key, WatchMode mode, List<RegistryKeySnapshot> result)
    {
        var snapshot = new RegistryKeySnapshot { Path = fullPath };
        foreach (var name in key.GetValueNames())
        {
            var kind = key.GetValueKind(name);
            // DoNotExpandEnvironmentNames keeps REG_EXPAND_SZ raw
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            snapshot.Values.Add(new RegistryValueData
            {
                Name = name,
                Kind = kind,
                Data = RegistryValueCodec.Encode(value, kind)
            });
        }

        // Default value may exist without being in GetValueNames on some systems;
        // also capture explicitly named empty default if present.
        try
        {
            var defaultKind = key.GetValueKind("");
            if (snapshot.Values.All(v => v.Name != ""))
            {
                var value = key.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                snapshot.Values.Add(new RegistryValueData
                {
                    Name = "",
                    Kind = defaultKind,
                    Data = RegistryValueCodec.Encode(value, defaultKind)
                });
            }
        }
        catch (IOException)
        {
            // no default value
        }

        snapshot.Values = snapshot.Values
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Add(snapshot);

        if (mode != WatchMode.Recursive)
            return;

        foreach (var subName in key.GetSubKeyNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var sub = key.OpenSubKey(subName);
                if (sub is null)
                    continue;
                CaptureKey(RegistryPathHelper.Combine(fullPath, subName), sub, mode, result);
            }
            catch (System.Security.SecurityException)
            {
                // skip inaccessible keys
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public LocationDiff Compare(WatchedLocation location)
    {
        List<RegistryKeySnapshot> current;
        try
        {
            current = Capture(location);
        }
        catch (InvalidOperationException)
        {
            // Entire watched root missing
            current = [];
        }

        var changes = DiffEngine.Compare(location.Keys, current);
        return new LocationDiff
        {
            Location = location,
            Changes = changes,
            CurrentSnapshot = current
        };
    }

    public void Revert(WatchedLocation location)
    {
        if (location.Mode == WatchMode.SingleValue)
        {
            foreach (var snap in location.Keys)
                RestoreSingleValue(snap, location.ValueName ?? string.Empty);
            return;
        }

        var targetPaths = location.Keys.Select(k => k.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove keys that exist now but were not in snapshot (deepest first)
        if (location.Mode == WatchMode.Recursive)
        {
            List<RegistryKeySnapshot> current;
            try
            {
                current = Capture(location);
            }
            catch (InvalidOperationException)
            {
                current = [];
            }

            foreach (var extra in current
                         .Where(k => !targetPaths.Contains(k.Path))
                         .OrderByDescending(k => k.Path.Length))
            {
                TryDeleteKey(extra.Path);
            }
        }

        // Recreate / restore keys (parents first)
        foreach (var snap in location.Keys.OrderBy(k => k.Path.Length))
        {
            RestoreKey(snap);
        }
    }

    /// <summary>指定した差分だけをスナップショット側の内容でレジストリに戻す。</summary>
    public void RevertChanges(WatchedLocation location, IReadOnlyList<DiffChange> changes)
    {
        foreach (var change in changes.OrderByDescending(c => c.KeyPath.Length))
        {
            switch (change.Kind)
            {
                case DiffChangeKind.KeyAdded:
                    TryDeleteKey(change.KeyPath);
                    break;
                case DiffChangeKind.KeyRemoved:
                {
                    var snap = location.Keys.FirstOrDefault(k =>
                        string.Equals(k.Path, change.KeyPath, StringComparison.OrdinalIgnoreCase));
                    if (snap is not null)
                        RestoreKey(snap);
                    break;
                }
                case DiffChangeKind.ValueAdded:
                    TryDeleteValue(change.KeyPath, change.ValueName ?? string.Empty);
                    break;
                case DiffChangeKind.ValueRemoved:
                case DiffChangeKind.ValueModified:
                {
                    var snap = location.Keys.FirstOrDefault(k =>
                        string.Equals(k.Path, change.KeyPath, StringComparison.OrdinalIgnoreCase));
                    if (snap is not null)
                        RestoreSingleValue(snap, change.ValueName ?? string.Empty);
                    break;
                }
            }
        }
    }

    /// <summary>ACCEPT した差分だけ、現在値をスナップショットへ取り込む。</summary>
    public static void AcceptChangesIntoSnapshot(
        WatchedLocation location,
        LocationDiff diff,
        IReadOnlyList<DiffChange> accepted)
    {
        if (accepted.Count == 0)
            return;

        var keyMap = location.Keys.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);
        var currentMap = diff.CurrentSnapshot.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var change in accepted)
        {
            switch (change.Kind)
            {
                case DiffChangeKind.KeyAdded:
                    if (currentMap.TryGetValue(change.KeyPath, out var added))
                        keyMap[change.KeyPath] = CloneKey(added);
                    break;
                case DiffChangeKind.KeyRemoved:
                    keyMap.Remove(change.KeyPath);
                    break;
                case DiffChangeKind.ValueAdded:
                case DiffChangeKind.ValueModified:
                    EnsureKey(keyMap, change.KeyPath);
                    if (currentMap.TryGetValue(change.KeyPath, out var curKey))
                    {
                        var curVal = curKey.Values.FirstOrDefault(v =>
                            string.Equals(v.Name, change.ValueName ?? "", StringComparison.OrdinalIgnoreCase));
                        if (curVal is not null)
                            UpsertValue(keyMap[change.KeyPath], curVal);
                    }
                    break;
                case DiffChangeKind.ValueRemoved:
                    if (keyMap.TryGetValue(change.KeyPath, out var snapKey))
                    {
                        snapKey.Values.RemoveAll(v =>
                            string.Equals(v.Name, change.ValueName ?? "", StringComparison.OrdinalIgnoreCase));
                    }
                    break;
            }
        }

        location.Keys = keyMap.Values.OrderBy(k => k.Path, StringComparer.OrdinalIgnoreCase).ToList();
        location.CapturedAt = DateTime.Now;
    }

    private static void EnsureKey(Dictionary<string, RegistryKeySnapshot> map, string path)
    {
        if (!map.ContainsKey(path))
            map[path] = new RegistryKeySnapshot { Path = path };
    }

    private static void UpsertValue(RegistryKeySnapshot key, RegistryValueData value)
    {
        var idx = key.Values.FindIndex(v =>
            string.Equals(v.Name, value.Name, StringComparison.OrdinalIgnoreCase));
        var clone = new RegistryValueData { Name = value.Name, Kind = value.Kind, Data = value.Data };
        if (idx >= 0)
            key.Values[idx] = clone;
        else
            key.Values.Add(clone);
    }

    private static RegistryKeySnapshot CloneKey(RegistryKeySnapshot src) => new()
    {
        Path = src.Path,
        Values = src.Values.Select(v => new RegistryValueData
        {
            Name = v.Name,
            Kind = v.Kind,
            Data = v.Data
        }).ToList()
    };

    private static void TryDeleteValue(string keyPath, string valueName)
    {
        using var key = RegistryPathHelper.OpenKey(keyPath, writable: true);
        if (key is null)
            return;
        try
        {
            key.DeleteValue(valueName, false);
        }
        catch
        {
            // already absent
        }
    }

    private static void RestoreSingleValue(RegistryKeySnapshot snap, string valueName)
    {
        if (!RegistryPathHelper.TryParse(snap.Path, out var hive, out var subKey))
            throw new InvalidOperationException($"Invalid path: {snap.Path}");

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        RegistryKey key;
        var ownsKey = false;
        if (string.IsNullOrEmpty(subKey))
        {
            key = baseKey;
        }
        else
        {
            key = baseKey.CreateSubKey(subKey, true)
                 ?? throw new InvalidOperationException($"Could not create key: {snap.Path}");
            ownsKey = true;
        }

        try
        {
            var target = snap.Values.FirstOrDefault(v =>
                string.Equals(v.Name, valueName, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                try
                {
                    key.DeleteValue(valueName, false);
                }
                catch
                {
                    // already absent
                }
                return;
            }

            var decoded = RegistryValueCodec.Decode(target.Data, target.Kind);
            if (decoded is null && target.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                return;
            key.SetValue(target.Name, decoded ?? string.Empty, target.Kind);
        }
        finally
        {
            if (ownsKey)
                key.Dispose();
        }
    }

    private static void RestoreKey(RegistryKeySnapshot snap)
    {
        if (!RegistryPathHelper.TryParse(snap.Path, out var hive, out var subKey))
            throw new InvalidOperationException($"Invalid path: {snap.Path}");

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        RegistryKey key;
        var ownsKey = false;
        if (string.IsNullOrEmpty(subKey))
        {
            key = baseKey;
        }
        else
        {
            key = baseKey.CreateSubKey(subKey, true)
                 ?? throw new InvalidOperationException($"Could not create key: {snap.Path}");
            ownsKey = true;
        }

        try
        {
            foreach (var existing in key.GetValueNames())
            {
                if (snap.Values.All(v => !string.Equals(v.Name, existing, StringComparison.OrdinalIgnoreCase)))
                    key.DeleteValue(existing, false);
            }

            try
            {
                _ = key.GetValueKind("");
                if (snap.Values.All(v => v.Name != ""))
                    key.DeleteValue("", false);
            }
            catch (IOException)
            {
                // no default value
            }

            foreach (var value in snap.Values)
            {
                var decoded = RegistryValueCodec.Decode(value.Data, value.Kind);
                if (decoded is null && value.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                    continue;
                key.SetValue(value.Name, decoded ?? string.Empty, value.Kind);
            }
        }
        finally
        {
            if (ownsKey)
                key.Dispose();
        }
    }

    private static void TryDeleteKey(string path)
    {
        if (!RegistryPathHelper.TryParse(path, out var hive, out var subKey) || string.IsNullOrEmpty(subKey))
            return;

        var parentSep = subKey.LastIndexOf('\\');
        var parentPath = parentSep < 0 ? string.Empty : subKey[..parentSep];
        var name = parentSep < 0 ? subKey : subKey[(parentSep + 1)..];

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            if (string.IsNullOrEmpty(parentPath))
            {
                baseKey.DeleteSubKeyTree(name, false);
            }
            else
            {
                using var parent = baseKey.OpenSubKey(parentPath, true);
                parent?.DeleteSubKeyTree(name, false);
            }
        }
        catch
        {
            // best effort
        }
    }
}

public static class DiffEngine
{
    public static List<DiffChange> Compare(
        IReadOnlyList<RegistryKeySnapshot> expected,
        IReadOnlyList<RegistryKeySnapshot> actual)
    {
        var changes = new List<DiffChange>();
        var expectedMap = expected.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);
        var actualMap = actual.ToDictionary(k => k.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in expectedMap.Keys.Except(actualMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.KeyRemoved,
                KeyPath = path
            });
        }

        foreach (var path in actualMap.Keys.Except(expectedMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.KeyAdded,
                KeyPath = path
            });
        }

        foreach (var path in expectedMap.Keys.Intersect(actualMap.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            CompareValues(expectedMap[path], actualMap[path], changes);
        }

        return changes;
    }

    private static void CompareValues(RegistryKeySnapshot expected, RegistryKeySnapshot actual, List<DiffChange> changes)
    {
        var exp = expected.Values.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        var act = actual.Values.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in exp.Keys.Except(act.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var v = exp[name];
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueRemoved,
                KeyPath = expected.Path,
                ValueName = name,
                OldValue = RegistryValueCodec.FormatForDisplay(v.Data, v.Kind),
                OldKind = v.Kind.ToString()
            });
        }

        foreach (var name in act.Keys.Except(exp.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var v = act[name];
            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueAdded,
                KeyPath = expected.Path,
                ValueName = name,
                NewValue = RegistryValueCodec.FormatForDisplay(v.Data, v.Kind),
                NewKind = v.Kind.ToString()
            });
        }

        foreach (var name in exp.Keys.Intersect(act.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var oldV = exp[name];
            var newV = act[name];
            if (oldV.Kind == newV.Kind && string.Equals(oldV.Data, newV.Data, StringComparison.Ordinal))
                continue;

            changes.Add(new DiffChange
            {
                Kind = DiffChangeKind.ValueModified,
                KeyPath = expected.Path,
                ValueName = name,
                OldValue = RegistryValueCodec.FormatForDisplay(oldV.Data, oldV.Kind),
                NewValue = RegistryValueCodec.FormatForDisplay(newV.Data, newV.Kind),
                OldKind = oldV.Kind.ToString(),
                NewKind = newV.Kind.ToString()
            });
        }
    }
}

public sealed class SnapshotStore
{
    private readonly string _filePath;

    public SnapshotStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MGA",
            "MGA Registry Checker");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "state.json");

        // 旧パスからの移行（存在する場合のみ）
        TryMigrateFromLegacyPath();
    }

    public string FilePath => _filePath;

    private void TryMigrateFromLegacyPath()
    {
        if (File.Exists(_filePath))
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] legacyPaths =
        [
            Path.Combine(localAppData, "MGA", "MGA RegistryChecker", "state.json"),
            Path.Combine(localAppData, "MGA-RegistryChecker", "state.json"),
        ];

        foreach (var legacy in legacyPaths)
        {
            if (!File.Exists(legacy))
                continue;

            try
            {
                File.Copy(legacy, _filePath);
                return;
            }
            catch
            {
                // 次の候補へ
            }
        }
    }

    public AppState Load()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        var json = File.ReadAllText(_filePath, Encoding.UTF8);
        return System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.AppState)
               ?? new AppState();
    }

    public void Save(AppState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state, AppJsonContext.Default.AppState);
        File.WriteAllText(_filePath, json, Encoding.UTF8);
    }
}
