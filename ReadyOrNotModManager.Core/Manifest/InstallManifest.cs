using System.Text.Json;

namespace ReadyOrNotModManager.Core.Manifest;

public sealed class InstallManifest
{
    public List<InstalledModRecord> Records { get; set; } = [];
}

public sealed class InstalledModRecord
{
    public string InstallId { get; set; } = Guid.NewGuid().ToString("N");
    public string ModName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
    public DateTimeOffset InstalledAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<string> DeployedFiles { get; set; } = [];
}

public sealed class InstallManifestStore(string manifestPath)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public InstallManifest Load()
    {
        if (!File.Exists(manifestPath))
        {
            return new InstallManifest();
        }

        using var stream = File.OpenRead(manifestPath);
        return JsonSerializer.Deserialize<InstallManifest>(stream, Options) ?? new InstallManifest();
    }

    public void Save(InstallManifest manifest)
    {
        var directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(manifestPath);
        JsonSerializer.Serialize(stream, manifest, Options);
    }
}
