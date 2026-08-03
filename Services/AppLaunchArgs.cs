namespace MgaRegistryChecker.Services;

/// <summary>コマンドライン引数の解釈。</summary>
public static class AppLaunchArgs
{
    /// <summary>
    /// メインウィンドウを出さず差分チェックのみ行う。
    /// 受け付ける形: --check /check -check --silent-check
    /// </summary>
    public static bool IsCheckOnly(IReadOnlyList<string> args) =>
        args.Any(IsCheckToken);

    private static bool IsCheckToken(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return false;

        var t = arg.Trim();
        return t.Equals("--check", StringComparison.OrdinalIgnoreCase)
               || t.Equals("/check", StringComparison.OrdinalIgnoreCase)
               || t.Equals("-check", StringComparison.OrdinalIgnoreCase)
               || t.Equals("--silent-check", StringComparison.OrdinalIgnoreCase);
    }
}
