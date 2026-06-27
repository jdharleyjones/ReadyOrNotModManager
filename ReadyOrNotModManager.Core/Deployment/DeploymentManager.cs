using ReadyOrNotModManager.Core.Archives;
using ReadyOrNotModManager.Core.Manifest;

namespace ReadyOrNotModManager.Core.Deployment;

public sealed record DeploymentRequest(
    string ModName,
    string SourceUrl,
    string ArchivePath,
    string ReadyOrNotInstallDirectory);

public sealed class DeploymentManager(InstallManifestStore manifestStore)
{
    public InstalledModRecord Deploy(DeploymentRequest request)
    {
        var paksDirectory = ReadyOrNotPaths.GetPaksDirectory(request.ReadyOrNotInstallDirectory);
        if (!Directory.Exists(paksDirectory))
        {
            throw new DirectoryNotFoundException($"Ready or Not Paks folder was not found at {paksDirectory}.");
        }

        var deployedFiles = ArchiveScanner.ExtractDeployableFiles(request.ArchivePath, paksDirectory);
        if (deployedFiles.Count == 0)
        {
            throw new InvalidOperationException("The selected archive does not contain Ready or Not mod files.");
        }

        var manifest = manifestStore.Load();
        var record = new InstalledModRecord
        {
            ModName = request.ModName,
            SourceUrl = request.SourceUrl,
            ArchivePath = request.ArchivePath,
            InstalledAtUtc = DateTimeOffset.UtcNow,
            DeployedFiles = deployedFiles.ToList()
        };
        manifest.Records.RemoveAll(existing => existing.SourceUrl.Equals(request.SourceUrl, StringComparison.OrdinalIgnoreCase));
        manifest.Records.Add(record);
        manifestStore.Save(manifest);
        return record;
    }

    public void Uninstall(string installId)
    {
        var manifest = manifestStore.Load();
        var record = manifest.Records.FirstOrDefault(item => item.InstallId == installId);
        if (record is null)
        {
            return;
        }

        foreach (var file in record.DeployedFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        manifest.Records.Remove(record);
        manifestStore.Save(manifest);
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
