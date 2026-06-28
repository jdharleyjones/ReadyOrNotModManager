using ReadyOrNotModManager.Core.Profiles;

namespace ReadyOrNotModManager.App.Services;

public sealed record ProfileExportState(bool CanExport, string Message)
{
    public static ProfileExportState FromProfiles(IEnumerable<ModProfile> profiles)
    {
        return profiles.Any()
            ? new ProfileExportState(true, "Choose a saved local modpack to export.")
            : new ProfileExportState(false, "No saved modpacks available to export.");
    }
}
