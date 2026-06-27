namespace ReadyOrNotModManager.App.Services;

public static class QueueDeploymentPlanner
{
    public static IReadOnlyList<ModQueueItem> GetDeployableDownloadedItems(IEnumerable<ModQueueItem> queue)
    {
        return queue
            .Where(item => !string.IsNullOrWhiteSpace(item.ArchivePath))
            .ToArray();
    }

    public static IReadOnlyList<ModQueueItem> RemoveSelectedItems(ICollection<ModQueueItem> queue, IEnumerable<ModQueueItem> selectedItems)
    {
        var removed = selectedItems.ToArray();
        foreach (var item in removed)
        {
            queue.Remove(item);
        }

        return removed;
    }
}
