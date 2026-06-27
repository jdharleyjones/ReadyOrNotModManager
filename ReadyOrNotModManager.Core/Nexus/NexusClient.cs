using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReadyOrNotModManager.Core.Nexus;

public sealed class NexusClient(HttpClient httpClient, string apiKey)
{
    private const string BaseRestUrl = "https://api.nexusmods.com/v1";

    public async Task<Uri> GetDownloadLinkAsync(string gameDomain, int modId, int fileId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseRestUrl}/games/{gameDomain}/mods/{modId}/files/{fileId}/download_link.json");
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new NexusApiException($"Nexus download link request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var links = await response.Content.ReadFromJsonAsync<List<DownloadLinkDto>>(cancellationToken).ConfigureAwait(false);
        var uri = links?.FirstOrDefault(link => !string.IsNullOrWhiteSpace(link.Uri))?.Uri;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var result))
        {
            throw new NexusApiException("Nexus did not return a usable download link.");
        }

        return result;
    }

    public async Task<IReadOnlyList<NexusModFile>> GetModFilesAsync(string gameDomain, int modId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseRestUrl}/games/{gameDomain}/mods/{modId}/files.json");
        AddHeaders(request);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new NexusApiException($"Nexus files request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var payload = await response.Content.ReadFromJsonAsync<FilesResponseDto>(cancellationToken).ConfigureAwait(false);
        return payload?.Files?
            .Where(file => file.CategoryId == 1 || file.IsPrimary)
            .Select(file => new NexusModFile(
                ModId: modId,
                FileId: file.FileId,
                Name: string.IsNullOrWhiteSpace(file.Name) ? $"Mod {modId} file {file.FileId}" : file.Name,
                Version: file.Version ?? string.Empty,
                SourceUrl: NexusUrlParser.CanonicalModUrl(gameDomain, modId)))
            .ToArray() ?? [];
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        request.Headers.UserAgent.ParseAdd("ReadyOrNotModManager/1.0");
    }

    private sealed class DownloadLinkDto
    {
        [JsonPropertyName("URI")]
        public string? Uri { get; set; }
    }

    private sealed class FilesResponseDto
    {
        [JsonPropertyName("files")]
        public List<FileDto>? Files { get; set; }
    }

    private sealed class FileDto
    {
        [JsonPropertyName("file_id")]
        public int FileId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("is_primary")]
        public bool IsPrimary { get; set; }
    }
}

public sealed class NexusApiException(string message) : Exception(message);
