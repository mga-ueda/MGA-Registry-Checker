using MgaRegistryChecker.Models;

namespace MgaRegistryChecker.ViewModels;

public sealed class WatchedLocationItem(WatchedLocation location)
{
    public Guid Id { get; } = location.Id;
    public string DisplayPath { get; } = UiText.FormatWatchPath(location);
    public string ModeLabel { get; } = location.Mode switch
    {
        WatchMode.KeyOnly => UiText.ModeKeyOnly,
        WatchMode.SingleValue => UiText.ModeSingleValue,
        _ => location.Mode.ToString()
    };
    public string KeyCount { get; } = location.Mode == WatchMode.SingleValue
            ? UiText.CountOneValue
            : UiText.CountKeys(location.Keys.Count);
    public string CapturedAtText { get; } = location.CapturedAt.ToString("yyyy/MM/dd HH:mm");
}
