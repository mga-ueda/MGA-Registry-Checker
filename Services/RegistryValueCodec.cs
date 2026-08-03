using Microsoft.Win32;

namespace MgaRegistryChecker.Services;

/// <summary>レジストリ値のスナップショット用エンコード / デコード。</summary>
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
            RegistryValueKind.MultiString => data.Length == 0 ? [] : data.Split('\n'),
            RegistryValueKind.DWord => uint.Parse(data),
            RegistryValueKind.QWord => ulong.Parse(data),
            RegistryValueKind.Binary or RegistryValueKind.None =>
                data.Length == 0 ? [] : Convert.FromBase64String(data),
            _ => Convert.FromBase64String(data)
        };
    }
}
