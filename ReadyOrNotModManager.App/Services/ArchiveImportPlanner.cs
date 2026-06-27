using System.IO;

namespace ReadyOrNotModManager.App.Services;

public static class ArchiveImportPlanner
{
    public static IReadOnlyList<ModQueueItem> ImportArchives(
        ICollection<ModQueueItem> queue,
        ModQueueItem? preferredItem,
        IReadOnlyList<string> archivePaths,
        string activeProfileId)
    {
        var imported = new List<ModQueueItem>();
        foreach (var archivePath in archivePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var item = preferredItem is not null && string.IsNullOrWhiteSpace(preferredItem.ArchivePath)
                ? preferredItem
                : CreateImportedQueueItem(archivePath, activeProfileId);

            if (!queue.Contains(item))
            {
                queue.Add(item);
            }

            item.ArchivePath = archivePath;
            item.Status = "Imported archive";
            imported.Add(item);
            preferredItem = null;
        }

        return imported;
    }

    private static ModQueueItem CreateImportedQueueItem(string archivePath, string activeProfileId)
    {
        return new ModQueueItem
        {
            ModName = Path.GetFileNameWithoutExtension(archivePath),
            Version = "Manual",
            SourceUrl = string.Empty,
            ProfileId = activeProfileId,
            Status = "Imported archive"
        };
    }
}
