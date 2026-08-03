using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace MgaRegistryChecker.Services;

/// <summary>GitHub Releases の最新版を調べ、実行中より新しければ通知用情報を返す。</summary>
public static class UpdateChecker
{
    public const string GitHubOwner = "mga-ueda";
    public const string GitHubRepo = "MGA-Registry-Checker";

    private static readonly Uri LatestReleaseApiUri =
        new($"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest");

    private static readonly HttpClient Http = CreateHttpClient();

    public sealed record ReleaseInfo(string DisplayVersion, string HtmlUrl);

    public static async Task<ReleaseInfo?> TryGetNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http
                .GetAsync(LatestReleaseApiUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var dto = await System.Text.Json.JsonSerializer
                .DeserializeAsync(stream, UpdateJsonContext.Default.GitHubLatestReleaseDto, cancellationToken)
                .ConfigureAwait(false);

            if (dto is null || string.IsNullOrWhiteSpace(dto.TagName) || string.IsNullOrWhiteSpace(dto.HtmlUrl))
                return null;

            if (dto.Draft)
                return null;

            if (!AppVersion.TryParse(dto.TagName, out var remote) ||
                !AppVersion.TryParse(AppVersion.GetDisplayVersion(), out var local))
                return null;

            if (remote <= local)
                return null;

            var display = dto.TagName.Trim();
            if (display.StartsWith('v') || display.StartsWith('V'))
                display = display[1..];

            return new ReleaseInfo(display, dto.HtmlUrl.Trim());
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            // オフライン・API 障害などは黙ってスキップ
            return null;
        }
    }

    /// <summary>
    /// UI スレッド向け。ネットワーク取得はスレッドプールで行い、デッドロックを避ける。
    /// </summary>
    public static ReleaseInfo? TryGetNewerReleaseBlocking(TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            return Task.Run(() => TryGetNewerReleaseAsync(cts.Token), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return null;
        }
    }

    public static void OpenReleasePage(string htmlUrl)
    {
        if (string.IsNullOrWhiteSpace(htmlUrl))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = htmlUrl,
            UseShellExecute = true
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MGA-Registry-Checker", AppVersion.GetDisplayVersion()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}

internal sealed class GitHubLatestReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GitHubLatestReleaseDto))]
internal partial class UpdateJsonContext : JsonSerializerContext;
