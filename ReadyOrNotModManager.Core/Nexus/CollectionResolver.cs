using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReadyOrNotModManager.Core.Nexus;

public sealed class CollectionResolver(HttpClient httpClient, string apiKey)
{
    private const string GraphQlUrl = "https://api.nexusmods.com/v2/graphql";

    public async Task<IReadOnlyList<NexusModFile>> ResolveLatestPublishedRevisionAsync(
        NexusCollectionReference collection,
        CancellationToken cancellationToken)
    {
        var (query, variables) = collection.RevisionNumber is null
            ? CreateLatestPublishedRevisionRequest(collection)
            : CreateExplicitRevisionRequest(collection);

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
        {
            Content = JsonContent.Create(new { query, variables })
        };
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        request.Headers.UserAgent.ParseAdd("ReadyOrNotModManager/1.0");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new NexusApiException($"Nexus collection request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CollectionResponse>(cancellationToken).ConfigureAwait(false);
        var modFiles = payload?.Data?.Collection?.LatestPublishedRevision?.ModFiles
            ?? payload?.Data?.CollectionRevision?.ModFiles
            ?? [];

        return modFiles
            .Where(item => item.File is not null)
            .Select(item => new NexusModFile(
                ModId: item.File!.ModId,
                FileId: item.File.FileId == 0 ? item.FileId : item.File.FileId,
                Name: $"{item.File.Mod?.Name ?? $"Mod {item.File.ModId}"} - {item.File.Name}",
                Version: item.File.Version ?? item.Version ?? string.Empty,
                SourceUrl: NexusUrlParser.CanonicalModUrl(collection.GameDomain, item.File.ModId)))
            .ToArray();
    }

    private static (string Query, object Variables) CreateLatestPublishedRevisionRequest(NexusCollectionReference collection)
    {
        return ("""
            query ReadyOrNotCollection($slug: String!, $domainName: String!) {
              collection(slug: $slug, domainName: $domainName) {
                latestPublishedRevision {
                  modFiles {
                    fileId
                    version
                    file {
                      fileId
                      modId
                      name
                      version
                      mod { name }
                    }
                  }
                }
              }
            }
            """,
            new
            {
                slug = collection.Slug,
                domainName = collection.GameDomain
            });
    }

    private static (string Query, object Variables) CreateExplicitRevisionRequest(NexusCollectionReference collection)
    {
        return ("""
            query ReadyOrNotCollectionRevision($slug: String!, $revisionNumber: Int!, $domainName: String!) {
              collectionRevision(slug: $slug, revisionNumber: $revisionNumber, domainName: $domainName) {
                modFiles {
                  fileId
                  version
                  file {
                    fileId
                    modId
                    name
                    version
                    mod { name }
                  }
                }
              }
            }
            """,
            new
            {
                slug = collection.Slug,
                revisionNumber = collection.RevisionNumber!.Value,
                domainName = collection.GameDomain
            });
    }

    private sealed class CollectionResponse
    {
        [JsonPropertyName("data")]
        public CollectionData? Data { get; set; }
    }

    private sealed class CollectionData
    {
        [JsonPropertyName("collection")]
        public CollectionPayload? Collection { get; set; }

        [JsonPropertyName("collectionRevision")]
        public CollectionRevision? CollectionRevision { get; set; }
    }

    private sealed class CollectionPayload
    {
        [JsonPropertyName("latestPublishedRevision")]
        public CollectionRevision? LatestPublishedRevision { get; set; }
    }

    private sealed class CollectionRevision
    {
        [JsonPropertyName("modFiles")]
        public List<CollectionModFile> ModFiles { get; set; } = [];
    }

    private sealed class CollectionModFile
    {
        [JsonPropertyName("fileId")]
        public int FileId { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("file")]
        public CollectionFile? File { get; set; }
    }

    private sealed class CollectionMod
    {
        [JsonPropertyName("modId")]
        public int ModId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CollectionFile
    {
        [JsonPropertyName("fileId")]
        public int FileId { get; set; }

        [JsonPropertyName("modId")]
        public int ModId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("mod")]
        public CollectionMod? Mod { get; set; }
    }
}
