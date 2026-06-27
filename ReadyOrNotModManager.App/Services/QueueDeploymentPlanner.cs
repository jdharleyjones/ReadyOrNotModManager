namespace ReadyOrNotModManager.App.Services;

public static class QueueDeploymentPlanner
{
    public static IReadOnlyList<ModQueueItem> GetDeployableDownloadedItems(IEnumerable<ModQueueItem> queue)
    {
        return queue
            .Where(item => !string.IsNullOrWhiteSpace(item.ArchivePath))
            .ToArray();
    }
}
