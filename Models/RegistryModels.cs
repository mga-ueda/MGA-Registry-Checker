using Microsoft.Win32;

namespace MGA_RegistryChecker.Models;

public enum WatchMode
{
    KeyOnly,
    Recursive,
    SingleValue
}

public enum DiffChangeKind
{
    KeyAdded,
    KeyRemoved,
    ValueAdded,
    ValueRemoved,
    ValueModified
}

public sealed class RegistryValueData
{
    public string Name { get; set; } = string.Empty;
    public RegistryValueKind Kind { get; set; }
    public string? Data { get; set; }
}

public sealed class RegistryKeySnapshot
{
    public string Path { get; set; } = string.Empty;
    public List<RegistryValueData> Values { get; set; } = [];
}

public sealed class WatchedLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    /// <summary>SingleValue のとき監視する値名。空文字は既定値。</summary>
    public string? ValueName { get; set; }
    public WatchMode Mode { get; set; } = WatchMode.KeyOnly;
    public DateTime CapturedAt { get; set; } = DateTime.Now;
    public List<RegistryKeySnapshot> Keys { get; set; } = [];
}

public sealed class AppState
{
    public List<WatchedLocation> Locations { get; set; } = [];
}

public sealed class DiffChange
{
    public DiffChangeKind Kind { get; set; }
    public string KeyPath { get; set; } = string.Empty;
    public string? ValueName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? OldKind { get; set; }
    public string? NewKind { get; set; }

    public string Summary => Kind switch
    {
        DiffChangeKind.KeyAdded => $"[Key added] {KeyPath}",
        DiffChangeKind.KeyRemoved => $"[Key removed] {KeyPath}",
        DiffChangeKind.ValueAdded => $"[Value added] {KeyPath}\\{DisplayName} = {NewValue} ({NewKind})",
        DiffChangeKind.ValueRemoved => $"[Value removed] {KeyPath}\\{DisplayName} = {OldValue} ({OldKind})",
        DiffChangeKind.ValueModified => $"[Value changed] {KeyPath}\\{DisplayName}\n  Old: {OldValue} ({OldKind})\n  New: {NewValue} ({NewKind})",
        _ => KeyPath
    };

    private string DisplayName => string.IsNullOrEmpty(ValueName) ? "(Default)" : ValueName;
}

public sealed class LocationDiff
{
    public WatchedLocation Location { get; set; } = null!;
    public List<DiffChange> Changes { get; set; } = [];
    public List<RegistryKeySnapshot> CurrentSnapshot { get; set; } = [];
}
