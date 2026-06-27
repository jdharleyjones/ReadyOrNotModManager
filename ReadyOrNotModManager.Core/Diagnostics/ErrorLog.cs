using System.Text.Json;

namespace ReadyOrNotModManager.Core.Diagnostics;

public sealed class ErrorLog
{
    public List<ErrorLogEntry> Entries { get; set; } = [];
}

public sealed class ErrorLogEntry
{
    public string ErrorId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Operation { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public int ModId { get; set; }
    public int FileId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
    public string ReadyOrNotDirectory { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class ErrorLogStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public ErrorLog Load()
    {
        if (!File.Exists(path))
        {
            return new ErrorLog();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<ErrorLog>(stream, Options) ?? new ErrorLog();
        }
        catch (JsonException)
        {
            return new ErrorLog();
        }
        catch (IOException)
        {
            return new ErrorLog();
        }
    }

    public void Append(ErrorLogEntry entry)
    {
        var log = Load();
        log.Entries.Insert(0, entry);
        Save(log);
    }

    public void Save(ErrorLog log)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, log, Options);
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
