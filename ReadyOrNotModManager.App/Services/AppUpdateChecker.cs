using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReadyOrNotModManager.App.Services;

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    UnableToCheck
}

public sealed record AppUpdateResult(AppUpdateStatus Status, string LatestTag, string Message);

public sealed class AppUpdateChecker(HttpClient httpClient)
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/jdharleyjones/ReadyOrNotModManager/releases/latest";

    public async Task<AppUpdateResult> CheckLatestAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("ReadyOrNotModManager");
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AppUpdateResult(AppUpdateStatus.UnableToCheck, string.Empty, "Unable to check for updates.");
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken).ConfigureAwait(false);
            var tag = release?.TagName ?? string.Empty;
            if (!TryParseVersion(tag, out var latestVersion))
            {
                return new AppUpdateResult(AppUpdateStatus.UnableToCheck, tag, "Unable to check for updates.");
            }

            if (NormalizeVersion(latestVersion) > NormalizeVersion(currentVersion))
            {
                return new AppUpdateResult(AppUpdateStatus.UpdateAvailable, tag, $"Update available: {tag}");
            }

            return new AppUpdateResult(AppUpdateStatus.UpToDate, tag, $"Up to date ({tag})");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new AppUpdateResult(AppUpdateStatus.UnableToCheck, string.Empty, "Unable to check for updates.");
        }
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var normalized = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out version!);
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }
}
