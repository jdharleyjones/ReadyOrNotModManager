using System.Text.RegularExpressions;

namespace ReadyOrNotModManager.Core.Nexus;

public static partial class NexusUrlParser
{
    public static NexusReference Parse(string input)
    {
        var trimmedInput = input.Trim();
        if (!Uri.TryCreate(trimmedInput, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Enter a full Nexus Mods URL.", nameof(input));
        }

        var path = NormalizePath(uri.AbsolutePath);
        var gameDomain = path[0].ToLowerInvariant();
        if (gameDomain != "readyornot")
        {
            throw new ArgumentException("Only Ready or Not Nexus Mods URLs are supported.", nameof(input));
        }

        if (path[1].Equals("mods", StringComparison.OrdinalIgnoreCase) &&
            path.Length >= 3 &&
            int.TryParse(path[2], out var modId))
        {
            return new NexusModReference(gameDomain, modId, CanonicalModUrl(gameDomain, modId));
        }

        if (path[1].Equals("collections", StringComparison.OrdinalIgnoreCase) && path.Length >= 3)
        {
            int? revision = null;
            if (path.Length >= 5 &&
                path[3].Equals("revisions", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(path[4], out var revisionNumber))
            {
                revision = revisionNumber;
            }

            if (!CollectionSlugRegex().IsMatch(path[2]))
            {
                throw new ArgumentException("The collection URL is missing a valid collection slug.", nameof(input));
            }

            return new NexusCollectionReference(gameDomain, path[2], revision, CanonicalCollectionUrl(gameDomain, path[2], revision));
        }

        throw new ArgumentException("Enter a Ready or Not mod or collection URL.", nameof(input));
    }

    private static string[] NormalizePath(string absolutePath)
    {
        var path = absolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length >= 4 && path[0].Equals("games", StringComparison.OrdinalIgnoreCase))
        {
            return path[1..];
        }

        if (path.Length >= 3)
        {
            return path;
        }

        throw new ArgumentException("Enter a Nexus Mods Ready or Not mod or collection URL.", nameof(absolutePath));
    }

    public static string CanonicalModUrl(string gameDomain, int modId)
    {
        return $"https://www.nexusmods.com/{gameDomain}/mods/{modId}";
    }

    public static string CanonicalCollectionUrl(string gameDomain, string slug, int? revisionNumber)
    {
        var url = $"https://www.nexusmods.com/{gameDomain}/collections/{slug}";
        return revisionNumber is null ? url : $"{url}/revisions/{revisionNumber.Value}";
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex CollectionSlugRegex();
}
