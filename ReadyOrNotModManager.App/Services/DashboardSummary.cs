using ReadyOrNotModManager.Core.Diagnostics;
using ReadyOrNotModManager.Core.Manifest;

namespace ReadyOrNotModManager.App.Services;

public sealed record DashboardSummary(
    int InstalledModCount,
    int PendingQueueCount,
    IReadOnlyList<RecentActivityItem> RecentActivity);

public sealed record RecentActivityItem(DateTimeOffset TimestampUtc, string Text);

public static class DashboardSummaryFactory
{
    public static DashboardSummary Create(InstallManifest manifest, IEnumerable<ModQueueItem> queue, ErrorLog log)
    {
        var pending = queue.Count(item =>
            item.Status is "Queued" or "Downloaded" or "Imported archive" or "Loaded from profile" or "Missing archive" or "Open Nexus page and import zip");

        var activity = log.Entries
            .Select(entry => new RecentActivityItem(entry.TimestampUtc, $"{entry.Operation} failed for {entry.ModName}"))
            .Concat(manifest.Records.Select(record => new RecentActivityItem(record.InstalledAtUtc, $"{record.ModName} deployed")))
            .OrderByDescending(item => item.TimestampUtc)
            .Take(8)
            .ToArray();

        return new DashboardSummary(manifest.Records.Count, pending, activity);
    }
}
