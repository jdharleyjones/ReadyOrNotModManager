using System.Text.Json;

namespace ReadyOrNotModManager.Core.Profiles;

public sealed class ModProfile
{
    public string ProfileId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New modpack";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ModProfileItem> Items { get; set; } = [];
}

public sealed class ModProfileItem
{
    public int ModId { get; set; }
    public int FileId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
    public List<string> SelectedArchiveEntries { get; set; } = [];
    public string LastInstallId { get; set; } = string.Empty;
}

public sealed class ModProfileStore(string libraryDirectory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public IReadOnlyList<ModProfile> LoadAll()
    {
        if (!Directory.Exists(libraryDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(libraryDirectory, "profile.json", SearchOption.AllDirectories)
            .Select(LoadFile)
            .Where(profile => profile is not null)
            .Cast<ModProfile>()
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ModProfile? Load(string profileId)
    {
        var path = GetProfilePath(profileId);
        return File.Exists(path) ? LoadFile(path) : null;
    }

    public ModProfile Save(ModProfile profile, bool copyArchives)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileId))
        {
            profile.ProfileId = Guid.NewGuid().ToString("N");
        }

        Directory.CreateDirectory(GetProfileDirectory(profile.ProfileId));
        if (copyArchives)
        {
            CopyArchivesIntoProfile(profile);
        }

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        using var stream = File.Create(GetProfilePath(profile.ProfileId));
        JsonSerializer.Serialize(stream, profile, Options);
        return profile;
    }

    public void Delete(string profileId)
    {
        var directory = GetProfileDirectory(profileId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public ProfileRenameResult Rename(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return new ProfileRenameResult(false, "Enter a modpack name.");
        }

        var profile = Load(profileId);
        if (profile is null)
        {
            return new ProfileRenameResult(false, "Select a modpack first.");
        }

        var trimmed = newName.Trim();
        if (LoadAll().Any(item =>
            !item.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase) &&
            item.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return new ProfileRenameResult(false, "A modpack with that name already exists.");
        }

        profile.Name = trimmed;
        Save(profile, copyArchives: false);
        return new ProfileRenameResult(true, string.Empty);
    }

    public string GetProfileDirectory(string profileId)
    {
        return Path.Combine(libraryDirectory, profileId);
    }

    private void CopyArchivesIntoProfile(ModProfile profile)
    {
        var archiveDirectory = Path.Combine(GetProfileDirectory(profile.ProfileId), "archives");
        Directory.CreateDirectory(archiveDirectory);

        foreach (var item in profile.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ArchivePath) || !File.Exists(item.ArchivePath))
            {
                continue;
            }

            var fullArchiveDirectory = Path.GetFullPath(archiveDirectory);
            var fullArchivePath = Path.GetFullPath(item.ArchivePath);
            if (fullArchivePath.StartsWith(fullArchiveDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = GetUniqueDestination(archiveDirectory, Path.GetFileName(item.ArchivePath));
            File.Copy(item.ArchivePath, destination, overwrite: false);
            item.ArchivePath = destination;
        }
    }

    private string GetProfilePath(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), "profile.json");
    }

    private static ModProfile? LoadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<ModProfile>(stream, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string GetUniqueDestination(string directory, string fileName)
    {
        var destination = Path.Combine(directory, SanitizeFileName(fileName));
        if (!File.Exists(destination))
        {
            return destination;
        }

        var stem = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var index = 1; ; index++)
        {
            var candidate = $"{stem}-{index}{extension}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "archive.zip" : sanitized;
    }
}

public sealed record ProfileRenameResult(bool Success, string Message);
