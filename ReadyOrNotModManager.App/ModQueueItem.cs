using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReadyOrNotModManager.App;

public sealed class ModQueueItem : INotifyPropertyChanged
{
    private string _status = "Queued";
    private string _archivePath = string.Empty;
    private string _installId = string.Empty;

    public int ModId { get; init; }
    public int FileId { get; init; }
    public string ModName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;

    public string ArchivePath
    {
        get => _archivePath;
        set => SetField(ref _archivePath, value);
    }

    public string InstallId
    {
        get => _installId;
        set => SetField(ref _installId, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
