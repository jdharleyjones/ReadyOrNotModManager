using SharpCompress.Archives;
using SharpCompress.Common;

namespace ReadyOrNotModManager.Core.Archives;

public sealed record DeployableArchiveFile(string EntryPath, string FileName);

public static class ArchiveScanner
{
    private static readonly HashSet<string> DeployableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pak",
        ".ucas",
        ".utoc",
        ".sig"
    };

    public static IReadOnlyList<DeployableArchiveFile> FindDeployableFiles(string archivePath)
    {
        ArchiveFormatDetector.Detect(archivePath);
        using var archive = OpenSupportedArchive(archivePath);
        var files = new List<DeployableArchiveFile>();

        foreach (var entry in archive.Entries)
        {
            var key = entry.Key;
            if (!entry.IsDirectory &&
                !string.IsNullOrWhiteSpace(key) &&
                DeployableExtensions.Contains(Path.GetExtension(key)))
            {
                files.Add(new DeployableArchiveFile(key.Replace('\\', '/'), Path.GetFileName(key)));
            }
        }

        return files;
    }

    public static IReadOnlyList<string> ExtractDeployableFiles(string archivePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        ArchiveFormatDetector.Detect(archivePath);
        using var archive = OpenSupportedArchive(archivePath);
        var deployed = new List<string>();

        foreach (var entry in archive.Entries)
        {
            var key = entry.Key;
            if (entry.IsDirectory ||
                string.IsNullOrWhiteSpace(key) ||
                !DeployableExtensions.Contains(Path.GetExtension(key)))
            {
                continue;
            }

            var destination = GetUniqueDestination(destinationDirectory, Path.GetFileName(key));
            using var input = entry.OpenEntryStream();
            using var output = File.Create(destination);
            input.CopyTo(output);
            deployed.Add(destination);
        }

        return deployed;
    }

    private static IArchive OpenSupportedArchive(string archivePath)
    {
        try
        {
            return ArchiveFactory.OpenArchive(archivePath, new SharpCompress.Readers.ReaderOptions());
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            throw new InvalidDataException(
                "The archive could not be read. Supported archive files are .zip, .rar, .7z, and .7zip. If this file was still downloading, download it again and import the completed archive.",
                ex);
        }
    }

    private static string GetUniqueDestination(string destinationDirectory, string fileName)
    {
        var destination = Path.Combine(destinationDirectory, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 1; ; index++)
        {
            destination = Path.Combine(destinationDirectory, $"{stem}-{index}{extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }
        }
    }
}
