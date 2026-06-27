using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ReadyOrNotModManager.Core.Deployment;
using ReadyOrNotModManager.Core.Diagnostics;

namespace ReadyOrNotModManager.App;

public partial class ErrorsWindow : Window
{
    private readonly ErrorLogStore _store;
    private readonly ObservableCollection<ErrorLogEntry> _entries = [];

    public ErrorsWindow(ErrorLogStore store)
    {
        InitializeComponent();
        _store = store;
        ErrorsGrid.ItemsSource = _entries;
        Reload();
    }

    private void Reload()
    {
        _entries.Clear();
        foreach (var entry in _store.Load().Entries)
        {
            _entries.Add(entry);
        }
    }

    private void OpenNexus_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.SourceUrl))
        {
            OpenPathOrUrl(entry.SourceUrl);
        }
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null || string.IsNullOrWhiteSpace(entry.ReadyOrNotDirectory))
        {
            return;
        }

        var paks = ReadyOrNotPaths.GetPaksDirectory(entry.ReadyOrNotDirectory);
        OpenPathOrUrl(Directory.Exists(paks) ? paks : entry.ReadyOrNotDirectory);
    }

    private void OpenArchiveFolder_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null || string.IsNullOrWhiteSpace(entry.ArchivePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(entry.ArchivePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            OpenPathOrUrl(directory);
        }
    }

    private void CopyManualFix_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            return;
        }

        var message = $"""
            {entry.ModName} could not be {entry.Operation.ToLowerInvariant()} automatically.

            Reason: {entry.Message}

            You may need to download this mod manually from Nexus Mods, then use Import archive in the manager or manually place the mod's .pak/.ucas/.utoc files into the Ready or Not Paks folder.

            Nexus page: {entry.SourceUrl}
            Archive: {entry.ArchivePath}
            Ready or Not folder: {entry.ReadyOrNotDirectory}
            """;
        System.Windows.Clipboard.SetText(message);
    }

    private void ClearErrors_Click(object sender, RoutedEventArgs e)
    {
        _store.Clear();
        Reload();
    }

    private ErrorLogEntry? GetSelectedEntry()
    {
        var entry = ErrorsGrid.SelectedItem as ErrorLogEntry;
        if (entry is null)
        {
            System.Windows.MessageBox.Show(this, "Select an error first.", "Ready or Not Mod Manager", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return entry;
    }

    private static void OpenPathOrUrl(string pathOrUrl)
    {
        Process.Start(new ProcessStartInfo(pathOrUrl.Trim()) { UseShellExecute = true });
    }
}
