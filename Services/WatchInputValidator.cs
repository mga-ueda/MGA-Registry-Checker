namespace MGA_RegistryChecker.Services;

/// <summary>メイン画面の Key / Value 入力検証。</summary>
public static class WatchInputValidator
{
    public readonly record struct Result(bool IsOk, string Message, bool IsIdle);

    public static Result Validate(string pathText, string valueText)
    {
        var path = pathText.Trim();
        var watchValue = !string.IsNullOrWhiteSpace(valueText);
        string? valueName = watchValue ? valueText.Trim() : null;

        if (string.IsNullOrWhiteSpace(path) && !watchValue)
            return new Result(false, UiText.ValidationHintIdle, IsIdle: true);

        var result = RegistryPathHelper.Validate(path, valueName);
        return new Result(result.IsOk, result.Message, IsIdle: false);
    }
}
