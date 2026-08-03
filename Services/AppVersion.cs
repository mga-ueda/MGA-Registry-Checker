using System.Reflection;

namespace MgaRegistryChecker.Services;

/// <summary>アセンブリからアプリの表示用バージョンを取得する。</summary>
public static class AppVersion
{
    public static string GetDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? info[..plus] : info;
        }

        var ver = asm.GetName().Version;
        return ver is null ? "1.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];

        var dash = s.IndexOf('-');
        if (dash >= 0)
            s = s[..dash];

        var plus = s.IndexOf('+');
        if (plus >= 0)
            s = s[..plus];

        return Version.TryParse(s, out version!);
    }
}
