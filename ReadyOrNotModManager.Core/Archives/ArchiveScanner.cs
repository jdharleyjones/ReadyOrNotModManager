using SharpCompress.Common;
using SharpCompress.Readers;

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
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream, new ReaderOptions());
        var files = new List<DeployableArchiveFile>();

        while (reader.MoveToNextEntry())
        {
            var key = reader.Entry.Key;
            if (!reader.Entry.IsDirectory &&
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
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream, new ReaderOptions());
        var deployed = new List<string>();

        while (reader.MoveToNextEntry())
        {
            var key = reader.Entry.Key;
            if (reader.Entry.IsDirectory ||
                string.IsNullOrWhiteSpace(key) ||
                !DeployableExtensions.Contains(Path.GetExtension(key)))
            {
                continue;
            }

            var destination = GetUniqueDestination(destinationDirectory, Path.GetFileName(key));
            reader.WriteEntryToFile(destination, new ExtractionOptions { ExtractFullPath = false, Overwrite = false });
            deployed.Add(destination);
        }

        return deployed;
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
