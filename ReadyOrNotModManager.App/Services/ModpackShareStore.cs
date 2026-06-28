using System.IO;
using System.Text.Json;
using ReadyOrNotModManager.Core.Profiles;

namespace ReadyOrNotModManager.App.Services;

public sealed class ModpackShareFile
{
    public string Format { get; set; } = ModpackShareStore.Format;
    public int FormatVersion { get; set; } = ModpackShareStore.FormatVersion;
    public string ModpackName { get; set; } = string.Empty;
    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ModpackShareItem> Items { get; set; } = [];
}

public sealed class ModpackShareItem
{
    public int ModId { get; set; }
    public int FileId { get; set; }
    public string ModName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}

public sealed record ModpackShareExportResult(int ExportedCount, int SkippedCount);

public static class ModpackShareStore
{
    public const string Format = "ReadyOrNotModManager.ModpackLinks";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static ModpackShareExportResult Export(ModProfile profile, string path, DateTimeOffset exportedAtUtc)
    {
        var items = profile.Items
            .Where(IsShareable)
            .Select(item => new ModpackShareItem
            {
                ModId = item.ModId,
                FileId = item.FileId,
                ModName = item.ModName,
                Version = item.Version,
                SourceUrl = item.SourceUrl
            })
            .ToList();

        var share = new ModpackShareFile
        {
            ModpackName = profile.Name,
            ExportedAtUtc = exportedAtUtc,
            Items = items
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, share, Options);
        return new ModpackShareExportResult(items.Count, profile.Items.Count - items.Count);
    }

    public static ModpackShareFile Import(string path)
    {
        using var stream = File.OpenRead(path);
        var share = JsonSerializer.Deserialize<ModpackShareFile>(stream, Options)
            ?? throw new InvalidDataException("The selected modpack share file is empty.");

        if (!share.Format.Equals(Format, StringComparison.OrdinalIgnoreCase) || share.FormatVersion != FormatVersion)
        {
            throw new InvalidDataException("The selected file is not a supported Ready or Not Mod Manager modpack share file.");
        }

        share.Items = share.Items.Where(IsShareable).ToList();
        if (share.Items.Count == 0)
        {
            throw new InvalidDataException("The selected modpack share file does not contain any Nexus mod links.");
        }

        if (string.IsNullOrWhiteSpace(share.ModpackName))
        {
            share.ModpackName = "Imported modpack";
        }

        return share;
    }

    public static ModProfile ToProfile(ModpackShareFile share, ModProfileStore profileStore, DateTimeOffset importedAtUtc)
    {
        var profileName = GetUniqueProfileName(share.ModpackName, profileStore, importedAtUtc);
        return new ModProfile
        {
            Name = profileName,
            Items = share.Items.Select(item => new ModProfileItem
            {
                ModId = item.ModId,
                FileId = item.FileId,
                ModName = item.ModName,
                Version = item.Version,
                SourceUrl = item.SourceUrl
            }).ToList()
        };
    }

    private static string GetUniqueProfileName(string name, ModProfileStore profileStore, DateTimeOffset importedAtUtc)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Imported modpack" : name.Trim();
        var existing = profileStore.LoadAll();
        if (existing.All(profile => !profile.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed;
        }

        return $"{trimmed} imported {importedAtUtc:yyyy-MM-dd HH-mm}";
    }

    private static bool IsShareable(ModProfileItem item)
    {
        return item.ModId > 0 && item.FileId > 0 && !string.IsNullOrWhiteSpace(item.SourceUrl);
    }

    private static bool IsShareable(ModpackShareItem item)
    {
        return item.ModId > 0 && item.FileId > 0 && !string.IsNullOrWhiteSpace(item.SourceUrl);
    }
}
