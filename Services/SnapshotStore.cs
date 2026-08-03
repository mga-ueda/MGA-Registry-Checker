using System.IO;
using System.Text;
using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.Services;

public sealed class SnapshotStore
{
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
        return System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.AppState)
               ?? new AppState();
    }

    public void Save(AppState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state, AppJsonContext.Default.AppState);
        File.WriteAllText(_filePath, json, Encoding.UTF8);
    }
}
