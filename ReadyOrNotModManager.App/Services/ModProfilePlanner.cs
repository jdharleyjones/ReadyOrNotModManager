using ReadyOrNotModManager.Core.Manifest;
using ReadyOrNotModManager.Core.Profiles;

namespace ReadyOrNotModManager.App.Services;

public static class ModProfilePlanner
{
    public static ModProfile FromInstalledRecords(ModProfile profile, IEnumerable<InstalledModRecord> records)
    {
        profile.Items = records
            .Where(record => record.DeployedFiles.Count > 0)
            .OrderBy(record => record.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.ModId)
            .ThenBy(record => record.FileId)
            .Select(record => new ModProfileItem
            {
                ModId = record.ModId,
                FileId = record.FileId,
                ModName = record.ModName,
                SourceUrl = record.SourceUrl,
                ArchivePath = record.ArchivePath,
                SelectedArchiveEntries = record.SelectedArchiveEntries.ToList(),
                LastInstallId = record.InstallId
            })
            .ToList();

        return profile;
    }
}
