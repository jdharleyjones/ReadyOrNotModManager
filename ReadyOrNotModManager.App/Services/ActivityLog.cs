using System.IO;
using System.Text.Json;

namespace ReadyOrNotModManager.App.Services;

public sealed class ActivityLog
{
    public List<ActivityLogEntry> Entries { get; set; } = [];
}

public sealed class ActivityLogEntry
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Message { get; set; } = string.Empty;
}

public sealed class ActivityLogStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public ActivityLog Load()
    {
        if (!File.Exists(path))
        {
            return new ActivityLog();
        }

        try
        {
            using var stream = File.OpenRead(path);
            var log = JsonSerializer.Deserialize<ActivityLog>(stream, Options) ?? new ActivityLog();
            log.Entries = log.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Message))
                .OrderByDescending(entry => entry.TimestampUtc)
                .Take(100)
                .ToList();
            return log;
        }
        catch (JsonException)
        {
            return new ActivityLog();
        }
        catch (IOException)
        {
            return new ActivityLog();
        }
    }

    public void Append(string message, DateTimeOffset? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var log = Load();
        log.Entries.Insert(0, new ActivityLogEntry
        {
            Message = message.Trim(),
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow
        });
        log.Entries = log.Entries.Take(100).ToList();
        Save(log);
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void Save(ActivityLog log)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, log, Options);
    }
}
