using ReadyOrNotModManager.Core.Archives;
using ReadyOrNotModManager.Core.Manifest;

namespace ReadyOrNotModManager.Core.Deployment;

public sealed record DeploymentRequest(
    string ModName,
    string SourceUrl,
    string ArchivePath,
    string ReadyOrNotInstallDirectory,
    string ProfileId = "",
    int ModId = 0,
    int FileId = 0,
    string ExistingInstallId = "",
    IReadOnlyCollection<string>? SelectedArchiveEntries = null,
    IProgress<double>? Progress = null);

public sealed class DeploymentManager(InstallManifestStore manifestStore)
{
    public InstalledModRecord Deploy(DeploymentRequest request)
    {
        var paksDirectory = ReadyOrNotPaths.GetPaksDirectory(request.ReadyOrNotInstallDirectory);
        if (!Directory.Exists(paksDirectory))
        {
            throw new DirectoryNotFoundException($"Ready or Not Paks folder was not found at {paksDirectory}.");
        }

        var manifest = manifestStore.Load();
        foreach (var existing in manifest.Records.Where(existing => ShouldReplace(existing, request)).ToArray())
        {
            DeleteDeployedFiles(existing);
            manifest.Records.Remove(existing);
        }

        var deployedFiles = ArchiveScanner.ExtractDeployableFiles(
            request.ArchivePath,
            paksDirectory,
            request.SelectedArchiveEntries,
            request.Progress);
        if (deployedFiles.Count == 0)
        {
            throw new InvalidOperationException("The selected archive does not contain Ready or Not mod files.");
        }

        var record = new InstalledModRecord
        {
            ModName = request.ModName,
            ModId = request.ModId,
            FileId = request.FileId,
            SourceUrl = request.SourceUrl,
            ArchivePath = request.ArchivePath,
            ProfileId = request.ProfileId,
            InstalledAtUtc = DateTimeOffset.UtcNow,
            SelectedArchiveEntries = request.SelectedArchiveEntries?.ToList() ?? [],
            DeployedFiles = deployedFiles.ToList()
        };
        manifest.Records.Add(record);
        manifestStore.Save(manifest);
        return record;
    }

    private static bool ShouldReplace(InstalledModRecord existing, DeploymentRequest request)
    {
        if (!existing.ProfileId.Equals(request.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.ExistingInstallId))
        {
            return existing.InstallId.Equals(request.ExistingInstallId, StringComparison.OrdinalIgnoreCase);
        }

        if (request.ModId > 0 && request.FileId > 0)
        {
            return existing.ModId == request.ModId && existing.FileId == request.FileId;
        }

        if (!string.IsNullOrWhiteSpace(request.ArchivePath))
        {
            return existing.ArchivePath.Equals(request.ArchivePath, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(request.SourceUrl) &&
            existing.SourceUrl.Equals(request.SourceUrl, StringComparison.OrdinalIgnoreCase);
    }

    public void Uninstall(string installId)
    {
        var manifest = manifestStore.Load();
        var record = manifest.Records.FirstOrDefault(item => item.InstallId == installId);
        if (record is null)
        {
            return;
        }

        DeleteDeployedFiles(record);
        manifest.Records.Remove(record);
        manifestStore.Save(manifest);
    }

    public void UninstallProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var manifest = manifestStore.Load();
        var records = manifest.Records
            .Where(item => item.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var record in records)
        {
            DeleteDeployedFiles(record);
            manifest.Records.Remove(record);
        }

        manifestStore.Save(manifest);
    }

    private static void DeleteDeployedFiles(InstalledModRecord record)
    {
        foreach (var file in record.DeployedFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}

public static class ReadyOrNotPaths
{
    public static string GetPaksDirectory(string installDirectory)
    {
        return Path.Combine(installDirectory, "ReadyOrNot", "Content", "Paks");
    }

    public static bool LooksLikeInstallDirectory(string installDirectory)
    {
        return Directory.Exists(GetPaksDirectory(installDirectory));
    }
}
