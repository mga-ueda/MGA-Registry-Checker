using System.IO;
using Microsoft.Win32;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

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
            throw new InvalidOperationException(UiText.ErrCouldNotOpenKey(path));

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
            // DoNotExpandEnvironmentNames で REG_EXPAND_SZ を展開せず生のまま取得
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            snapshot.Values.Add(new RegistryValueData
            {
                Name = name,
                Kind = kind,
                Data = RegistryValueCodec.Encode(value, kind)
            });
        }

        // 既定値は GetValueNames に出ない環境があるため、明示的にも取得する
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
            // 既定値なし
        }

        snapshot.Values = snapshot.Values
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Add(snapshot);

        if (mode == WatchMode.KeyOnly)
        {
            // 1階層のサブキー名のみ（存在監視）。中身やそれより深い階層は見ない。
            foreach (var subName in key.GetSubKeyNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(new RegistryKeySnapshot
                {
                    Path = RegistryPathHelper.Combine(fullPath, subName),
                    Values = []
                });
            }

            return;
        }

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
                // アクセスできないキーはスキップ
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
            // 監視ルート自体が存在しない
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

        // スナップショットに無いキーを削除（Recursive は深い階層、KeyOnly は直下1階層分）
        if (location.Mode is WatchMode.Recursive or WatchMode.KeyOnly)
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

        // キーを再作成・復元（親から先に）
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
            // 既に無い
        }
    }

    private static void RestoreSingleValue(RegistryKeySnapshot snap, string valueName)
    {
        if (!RegistryPathHelper.TryParse(snap.Path, out var hive, out var subKey))
            throw new InvalidOperationException(UiText.ErrInvalidPath(snap.Path));

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
                 ?? throw new InvalidOperationException(UiText.ErrCouldNotCreateKey(snap.Path));
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
                    // 既に無い
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
            throw new InvalidOperationException(UiText.ErrInvalidPath(snap.Path));

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
                 ?? throw new InvalidOperationException(UiText.ErrCouldNotCreateKey(snap.Path));
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
                // 既定値なし
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
            // 削除できなくても続行（ベストエフォート）
        }
    }
}
