using System.IO;
using System.Text.RegularExpressions;
using ReadyOrNotModManager.Core.Deployment;

namespace ReadyOrNotModManager.App.Services;

public static partial class ReadyOrNotInstallDetector
{
    private static readonly string[] GameFolderNames = ["Ready Or Not", "Ready or Not", "ReadyOrNot"];

    public static string? FindInstallDirectory(IEnumerable<string>? steamRoots = null)
    {
        var roots = steamRoots?.Where(Directory.Exists).ToArray() ?? GetDefaultSteamRoots();
        foreach (var candidate in roots.SelectMany(GetCandidateGameDirectories))
        {
            if (ReadyOrNotPaths.LooksLikeInstallDirectory(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateGameDirectories(string steamRoot)
    {
        foreach (var library in GetSteamLibraries(steamRoot).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var folderName in GameFolderNames)
            {
                yield return Path.Combine(library, "steamapps", "common", folderName);
            }
        }
    }

    private static IEnumerable<string> GetSteamLibraries(string steamRoot)
    {
        yield return steamRoot;
        var libraryFolders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFolders))
        {
            yield break;
        }

        var content = File.ReadAllText(libraryFolders);
        foreach (Match match in SteamPathRegex().Matches(content))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static string[] GetDefaultSteamRoots()
    {
        var roots = new List<string>();
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Steam"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Steam"));
        }

        roots.Add(@"C:\Steam");
        return roots.Where(Directory.Exists).ToArray();
    }

    [GeneratedRegex("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex SteamPathRegex();
}
