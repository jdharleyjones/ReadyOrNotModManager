using SharpCompress.Archives;
using SharpCompress.Common;

namespace ReadyOrNotModManager.Core.Archives;

public sealed record DeployableArchiveFile(string EntryPath, string FileName);

public sealed record DeployableArchiveGroup(string DisplayName, IReadOnlyList<DeployableArchiveFile> Files);

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

    public static IReadOnlyList<DeployableArchiveGroup> GetDeployableGroups(string archivePath)
    {
        var files = FindDeployableFiles(archivePath);
        var pakStems = files
            .Where(file => Path.GetExtension(file.FileName).Equals(".pak", StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetFileNameWithoutExtension(file.FileName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return pakStems
            .Select(stem => new DeployableArchiveGroup(
                stem,
                files
                    .Where(file => Path.GetFileNameWithoutExtension(file.FileName).Equals(stem, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => GetDeployableOrder(Path.GetExtension(file.FileName)))
                    .ThenBy(file => file.EntryPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    public static IReadOnlyList<string> ExtractDeployableFiles(
        string archivePath,
        string destinationDirectory,
        IReadOnlyCollection<string>? selectedEntryPaths = null,
        IProgress<double>? progress = null)
    {
        Directory.CreateDirectory(destinationDirectory);
        ArchiveFormatDetector.Detect(archivePath);
        using var archive = OpenSupportedArchive(archivePath);
        var deployed = new List<string>();
        var selected = selectedEntryPaths is null
            ? null
            : new HashSet<string>(selectedEntryPaths.Select(NormalizeEntryPath), StringComparer.OrdinalIgnoreCase);
        var entries = archive.Entries
            .Where(entry =>
            {
                var key = entry.Key;
                return !entry.IsDirectory &&
                    !string.IsNullOrWhiteSpace(key) &&
                    DeployableExtensions.Contains(Path.GetExtension(key)) &&
                    (selected is null || selected.Contains(NormalizeEntryPath(key)));
            })
            .ToArray();

        var totalBytes = entries.Sum(entry => Math.Max(entry.Size, 1));
        long copiedBytes = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var key = entry.Key;
            var destination = GetUniqueDestination(destinationDirectory, Path.GetFileName(key!));
            using var input = entry.OpenEntryStream();
            using var output = File.Create(destination);
            copiedBytes = CopyWithProgress(input, output, Math.Max(entry.Size, 1), copiedBytes, totalBytes, progress);
            deployed.Add(destination);
        }

        progress?.Report(1);
        return deployed;
    }

    private static long CopyWithProgress(
        Stream input,
        Stream output,
        long entrySize,
        long copiedBeforeEntry,
        long totalBytes,
        IProgress<double>? progress)
    {
        var buffer = new byte[81920];
        long copiedInEntry = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            copiedInEntry += read;
            if (totalBytes > 0)
            {
                progress?.Report(Math.Clamp((copiedBeforeEntry + Math.Min(copiedInEntry, entrySize)) / (double)totalBytes, 0, 1));
            }
        }

        return copiedBeforeEntry + entrySize;
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

    private static int GetDeployableOrder(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pak" => 0,
            ".ucas" => 1,
            ".utoc" => 2,
            ".sig" => 3,
            _ => 4
        };
    }

    private static string NormalizeEntryPath(string entryPath)
    {
        return entryPath.Replace('\\', '/');
    }
}
