using Microsoft.Win32;
using MGA_RegistryChecker.Models;

namespace MGA_RegistryChecker.Services;

/// <summary>DEBUG 用: 実レジストリを触れない擬似差分を生成する。</summary>
public static class DiffSimulator
{
    private const int DemoChangeCount = 20;

    private static readonly string[] SampleRoots =
    [
        @"HKEY_CURRENT_USER\Software\MGA-RegistryChecker\Demo",
        @"HKEY_CURRENT_USER\Control Panel\Desktop",
        @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager",
    ];

    private static readonly string[] SampleValues =
    [
        "BorderWidth", "PaddedBorderWidth", "AppliedDPI", "Wallpaper", "UserPreferencesMask",
        "DisplayName", "Timeout", "Enabled", "SimFlag", "InstallPath",
        "LastUpdate", "Version", "Locale", "ThemeColor", "AutoCheck",
        "CacheSize", "MaxItems", "LogLevel", "Endpoint", "FeatureToggle"
    ];

    /// <summary>差分ダイアログ UI 確認用の擬似差分（約 20 件）。</summary>
    public static LocationDiff CreateRandom(IReadOnlyList<WatchedLocation>? existing = null)
    {
        var rng = Random.Shared;
        var path = existing is { Count: > 0 }
            ? existing[rng.Next(existing.Count)].Path
            : SampleRoots[rng.Next(SampleRoots.Length)];

        var location = existing?.FirstOrDefault(l => l.Path == path)
                       ?? new WatchedLocation
                       {
                           Path = path,
                           Mode = WatchMode.KeyOnly,
                           CapturedAt = DateTime.Now.AddHours(-rng.Next(1, 48)),
                           Keys =
                           [
                               new RegistryKeySnapshot
                               {
                                   Path = path,
                                   Values =
                                   [
                                       new RegistryValueData
                                       {
                                           Name = "Baseline",
                                           Kind = RegistryValueKind.String,
                                           Data = "snapshot"
                                       }
                                   ]
                               }
                           ]
                       };

        var kinds = Enum.GetValues<DiffChangeKind>();
        var changes = new List<DiffChange>(DemoChangeCount);

        for (var i = 0; i < DemoChangeCount; i++)
        {
            var kind = kinds[i % kinds.Length];
            var keyPath = i % 4 == 0
                ? $"{path}\\SimSub{i + 1:D2}"
                : path;
            var valueName = $"{SampleValues[i % SampleValues.Length]}_{i + 1:D2}";

            changes.Add(kind switch
            {
                DiffChangeKind.KeyAdded => new DiffChange
                {
                    Kind = kind,
                    KeyPath = keyPath
                },
                DiffChangeKind.KeyRemoved => new DiffChange
                {
                    Kind = kind,
                    KeyPath = keyPath
                },
                DiffChangeKind.ValueAdded => new DiffChange
                {
                    Kind = kind,
                    KeyPath = keyPath,
                    ValueName = valueName,
                    NewValue = DemoValue(i, rng),
                    NewKind = DemoKind(i)
                },
                DiffChangeKind.ValueRemoved => new DiffChange
                {
                    Kind = kind,
                    KeyPath = keyPath,
                    ValueName = valueName,
                    OldValue = DemoValue(i, rng),
                    OldKind = DemoKind(i)
                },
                _ => new DiffChange
                {
                    Kind = DiffChangeKind.ValueModified,
                    KeyPath = keyPath,
                    ValueName = valueName,
                    OldValue = DemoValue(i, rng),
                    NewValue = DemoValue(i + 11, rng),
                    OldKind = DemoKind(i),
                    NewKind = DemoKind(i + 1)
                }
            });
        }

        return new LocationDiff
        {
            Location = location,
            Changes = changes,
            CurrentSnapshot =
            [
                new RegistryKeySnapshot
                {
                    Path = path,
                    Values =
                    [
                        new RegistryValueData
                        {
                            Name = "Simulated",
                            Kind = RegistryValueKind.String,
                            Data = "current"
                        }
                    ]
                }
            ]
        };
    }

    private static string DemoValue(int index, Random rng) => (index % 5) switch
    {
        0 => rng.Next(-10, 50).ToString(),
        1 => $"0x{rng.Next(0, 0xFFFF):X4}",
        2 => $"C:\\Program Files\\Demo\\App{index:D2}\\config.xml",
        3 => $"long-text-value-for-column-width-check-{index:D2}-{Guid.NewGuid():N}"[..48],
        _ => $"text-{100 + index}"
    };

    private static string DemoKind(int index) => (index % 3) switch
    {
        0 => "String",
        1 => "DWord",
        _ => "ExpandString"
    };
}
