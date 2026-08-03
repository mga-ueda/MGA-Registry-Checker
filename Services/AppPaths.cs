using System.IO;

namespace MGA_RegistryChecker.Services;

/// <summary>アプリ名・状態ファイルパスなど、永続化関連の定数。</summary>
public static class AppPaths
{
    public const string CompanyFolder = "MGA";
    public const string AppFolder = "MGA Registry Checker";
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
