using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

public sealed partial class SnapshotStore
{
    [GeneratedRegex("\"mode\"\\s*:\\s*\"Recursive\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyRecursiveMode();

    private readonly string _filePath;

    public SnapshotStore()
    {
        _filePath = AppPaths.GetStateFilePath();
    }

    public string FilePath => _filePath;

    public AppState Load()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        var json = File.ReadAllText(_filePath, Encoding.UTF8);
        // 旧 WatchMode.Recursive は KeyOnly 相当として読み込む
        json = LegacyRecursiveMode().Replace(json, "\"mode\":\"KeyOnly\"");
        return System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.AppState)
               ?? new AppState();
    }

    public void Save(AppState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state, AppJsonContext.Default.AppState);
        File.WriteAllText(_filePath, json, Encoding.UTF8);
    }
}
