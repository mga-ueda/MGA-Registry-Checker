using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.ViewModels;

public sealed class WatchedLocationItem
{
    public WatchedLocationItem(WatchedLocation location)
    {
        Id = location.Id;
        DisplayPath = location.Mode == WatchMode.SingleValue
            ? UiText.SingleValueDisplayPath(location.Path, location.ValueName)
            : location.Path;
        ModeLabel = location.Mode switch
        {
            WatchMode.Recursive => UiText.ModeRecursive,
            WatchMode.KeyOnly => UiText.ModeKeyOnly,
            WatchMode.SingleValue => UiText.ModeSingleValue,
            _ => location.Mode.ToString()
        };
        KeyCount = location.Mode == WatchMode.SingleValue
            ? UiText.CountOneValue
            : UiText.CountKeys(location.Keys.Count);
        CapturedAtText = location.CapturedAt.ToString("yyyy/MM/dd HH:mm");
    }

    public Guid Id { get; }
    public string DisplayPath { get; }
    public string ModeLabel { get; }
    public string KeyCount { get; }
    public string CapturedAtText { get; }
}
