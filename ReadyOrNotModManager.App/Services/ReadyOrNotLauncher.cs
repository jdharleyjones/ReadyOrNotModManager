using System.IO;

namespace ReadyOrNotModManager.App.Services;

public sealed record ReadyOrNotLaunchTarget(
    bool CanLaunch,
    string Target,
    string WorkingDirectory,
    bool UseShellExecute,
    string Message);

public static class ReadyOrNotLauncher
{
    public const string SteamLaunchUri = "steam://rungameid/1144200";

    private static readonly string[] ExecutableCandidates =
    [
        Path.Combine("ReadyOrNot", "Binaries", "Win64", "ReadyOrNotSteam-Win64-Shipping.exe"),
        Path.Combine("ReadyOrNot", "Binaries", "Win64", "ReadyOrNot-Win64-Shipping.exe"),
        Path.Combine("ReadyOrNot", "Binaries", "Win64", "ReadyOrNot.exe"),
        "ReadyOrNot.exe"
    ];

    public static ReadyOrNotLaunchTarget Resolve(string installDirectory, bool preferSteam)
    {
        if (preferSteam)
        {
            return new ReadyOrNotLaunchTarget(true, SteamLaunchUri, string.Empty, true, "Launching Ready or Not through Steam.");
        }

        var executablePath = FindDirectExecutable(installDirectory);
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return new ReadyOrNotLaunchTarget(
                true,
                executablePath,
                Path.GetDirectoryName(executablePath) ?? installDirectory,
                true,
                "Launching Ready or Not.");
        }

        return new ReadyOrNotLaunchTarget(
            false,
            string.Empty,
            string.Empty,
            true,
            "Ready or Not executable was not found. Check the game install folder in Settings.");
    }

    public static string? FindDirectExecutable(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return null;
        }

        foreach (var candidate in ExecutableCandidates)
        {
            var path = Path.Combine(installDirectory, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
