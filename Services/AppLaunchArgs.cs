namespace MgaRegistryChecker.Services;

/// <summary>コマンドライン引数の解釈。</summary>
public static class AppLaunchArgs
{
    /// <summary>スタートアップ登録やドキュメントで使う正式なチェック専用引数。</summary>
    public const string CheckArgument = "--check";

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
        return t.Equals(CheckArgument, StringComparison.OrdinalIgnoreCase)
               || t.Equals("/check", StringComparison.OrdinalIgnoreCase)
               || t.Equals("-check", StringComparison.OrdinalIgnoreCase)
               || t.Equals("--silent-check", StringComparison.OrdinalIgnoreCase);
    }
}
