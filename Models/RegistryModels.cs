using Microsoft.Win32;

namespace MgaRegistryChecker.Models;

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

    /// <summary>設定（監視追加）用メインウィンドウの位置・サイズ。差分ダイアログは保存しない。</summary>
    public WindowBounds? MainWindowBounds { get; set; }
}

/// <summary>ウィンドウの位置とサイズ。</summary>
public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
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
}

public sealed class LocationDiff
{
    public WatchedLocation Location { get; set; } = null!;
    public List<DiffChange> Changes { get; set; } = [];
    public List<RegistryKeySnapshot> CurrentSnapshot { get; set; } = [];
}
