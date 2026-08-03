using System.IO;

namespace MgaRegistryChecker.Services;

/// <summary>アプリ名・状態ファイルパスなど、永続化関連の定数。</summary>
public static class AppPaths
{
    public const string CompanyFolder = "MGA";

    /// <summary>正式なアプリ表示名（LocalAppData フォルダ名にも使う）。</summary>
    public const string AppDisplayName = "MGA Registry Checker";

    public const string AppFolder = AppDisplayName;
    public const string StateFileName = "state.json";

    public static string GetStateDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            CompanyFolder,
            AppFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetStateFilePath() =>
        Path.Combine(GetStateDirectory(), StateFileName);
}
