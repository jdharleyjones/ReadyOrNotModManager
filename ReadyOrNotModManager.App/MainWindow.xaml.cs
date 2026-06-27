using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using ReadyOrNotModManager.Core.Deployment;
using ReadyOrNotModManager.Core.Downloads;
using ReadyOrNotModManager.Core.Manifest;
using ReadyOrNotModManager.Core.Nexus;
using Forms = System.Windows.Forms;

namespace ReadyOrNotModManager.App;

public partial class MainWindow : Window
{
    private const string NexusApiKeyPage = "https://www.nexusmods.com/users/myaccount?tab=api%20access";
    private readonly ObservableCollection<ModQueueItem> _queue = [];
    private readonly LocalSettingsStore _settingsStore;
    private readonly string _appDataDirectory;
    private LocalSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        _appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadyOrNotModManager");
        _settingsStore = new LocalSettingsStore(_appDataDirectory);
        QueueGrid.ItemsSource = _queue;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _settings = _settingsStore.Load();
        ApiKeyBox.Password = _settings.ApiKey;
        DownloadDirectoryBox.Text = _settings.DownloadDirectory;
        GameDirectoryBox.Text = _settings.ReadyOrNotDirectory;
        ImportDirectoryBox.Text = _settings.ImportDirectory;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        SetStatus("Settings saved.");
    }

    private void OpenApiKeyPage_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(NexusApiKeyPage);
        SetStatus("Opened Nexus API key page.");
    }

    private void BrowseDownloadDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(DownloadDirectoryBox.Text, out var selected))
        {
            DownloadDirectoryBox.Text = selected;
            SaveSettings();
        }
    }

    private void OpenDownloadDirectory_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Directory.CreateDirectory(_settings.DownloadDirectory);
        OpenFolder(_settings.DownloadDirectory);
        SetStatus("Opened download folder.");
    }

    private void BrowseGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(GameDirectoryBox.Text, out var selected))
        {
            GameDirectoryBox.Text = selected;
            SaveSettings();
            SetStatus(ReadyOrNotPaths.LooksLikeInstallDirectory(selected)
                ? "Ready or Not folder validated."
                : "Folder saved, but the Paks path was not found yet.");
        }
    }

    private void BrowseImportDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(ImportDirectoryBox.Text, out var selected))
        {
            ImportDirectoryBox.Text = selected;
            SaveSettings();
        }
    }

    private async void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            SaveSettings();
            var reference = NexusUrlParser.Parse(UrlBox.Text);
            using var http = new HttpClient();

            switch (reference)
            {
                case NexusModReference mod:
                    await AddModReferenceAsync(http, mod);
                    break;
                case NexusCollectionReference collection:
                    await AddCollectionReferenceAsync(http, collection);
                    break;
            }

            UrlBox.Clear();
        });
    }

    private async Task AddModReferenceAsync(HttpClient http, NexusModReference mod)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _queue.Add(new ModQueueItem
            {
                ModId = mod.ModId,
                FileId = 0,
                ModName = $"Mod {mod.ModId}",
                SourceUrl = mod.SourceUrl,
                Status = "Needs API key or browser import"
            });
            SetStatus("Added mod placeholder. Save an API key to resolve files automatically.");
            return;
        }

        var client = new NexusClient(http, _settings.ApiKey);
        var files = await client.GetModFilesAsync(mod.GameDomain, mod.ModId, CancellationToken.None);
        foreach (var file in files.DefaultIfEmpty(new NexusModFile(mod.ModId, 0, $"Mod {mod.ModId}", string.Empty, mod.SourceUrl)))
        {
            AddQueueItem(file);
        }

        SetStatus($"Added {files.Count} file(s) for mod {mod.ModId}.");
    }

    private async Task AddCollectionReferenceAsync(HttpClient http, NexusCollectionReference collection)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Save a Nexus API key before adding a collection.");
        }

        var resolver = new CollectionResolver(http, _settings.ApiKey);
        var files = await resolver.ResolveLatestPublishedRevisionAsync(collection, CancellationToken.None);
        foreach (var file in files)
        {
            AddQueueItem(file);
        }

        SetStatus($"Added {files.Count} collection item(s).");
    }

    private async void DownloadMissing_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            SaveSettings();
            Directory.CreateDirectory(_settings.DownloadDirectory);
            using var http = new HttpClient();
            var nexus = new NexusClient(http, _settings.ApiKey);
            var downloader = new DownloadManager(http);

            foreach (var item in _queue.Where(item => string.IsNullOrWhiteSpace(item.ArchivePath)).ToArray())
            {
                if (item.FileId <= 0 || string.IsNullOrWhiteSpace(_settings.ApiKey))
                {
                    item.Status = "Open Nexus page and import zip";
                    OpenUrl(item.SourceUrl);
                    continue;
                }

                try
                {
                    item.Status = "Requesting download link";
                    var downloadUri = await nexus.GetDownloadLinkAsync("readyornot", item.ModId, item.FileId, CancellationToken.None);
                    item.Status = "Downloading";
                    item.ArchivePath = await downloader.DownloadAsync(
                        downloadUri,
                        _settings.DownloadDirectory,
                        BuildArchiveFileName(item),
                        null,
                        CancellationToken.None);
                    item.Status = "Downloaded";
                }
                catch (Exception ex) when (ex is NexusApiException or HttpRequestException or InvalidDataException)
                {
                    item.Status = "Browser fallback required";
                    OpenUrl(item.SourceUrl);
                }
            }

            SetStatus("Download pass complete.");
        });
    }

    private void OpenSelectedPage_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in GetSelectedItems())
        {
            OpenUrl(item.SourceUrl);
            item.Status = "Opened in browser";
        }
    }

    private void ImportArchive_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var item = GetSingleSelectedItem() ?? (_queue.Count == 1 ? _queue[0] : null);

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select downloaded mod archive",
            Filter = "Mod archives (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_settings.ImportDirectory) ? _settings.ImportDirectory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog(this) == true)
        {
            item ??= CreateImportedQueueItem(dialog.FileName);
            item.ArchivePath = dialog.FileName;
            item.Status = "Imported archive";
            ImportDirectoryBox.Text = Path.GetDirectoryName(dialog.FileName) ?? ImportDirectoryBox.Text;
            SaveSettings();
            QueueGrid.SelectedItem = item;
        }
    }

    private void DeleteDownloadedArchive_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems(requireSelection: true);
        if (items.Count == 0)
        {
            SetStatus("Select a queue item before deleting a downloaded archive.");
            return;
        }

        var archives = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ArchivePath) && File.Exists(item.ArchivePath))
            .ToArray();

        if (archives.Length == 0)
        {
            SetStatus("Selected item has no downloaded archive to delete.");
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"Delete {archives.Length} downloaded archive file(s)? This does not uninstall deployed mod files.",
            "Ready or Not Mod Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var item in archives)
        {
            File.Delete(item.ArchivePath);
            item.ArchivePath = string.Empty;
            item.Status = string.IsNullOrWhiteSpace(item.InstallId) ? "Download deleted" : "Deployed, download deleted";
        }

        SetStatus("Downloaded archive file(s) deleted.");
    }

    private async void DeploySelected_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(() =>
        {
            SaveSettings();
            if (!ReadyOrNotPaths.LooksLikeInstallDirectory(_settings.ReadyOrNotDirectory))
            {
                throw new DirectoryNotFoundException("Choose the Ready or Not install folder that contains ReadyOrNot\\Content\\Paks.");
            }

            var manager = CreateDeploymentManager();
            foreach (var item in GetSelectedItems())
            {
                if (!File.Exists(item.ArchivePath))
                {
                    item.Status = "Missing zip";
                    continue;
                }

                try
                {
                    var record = manager.Deploy(new DeploymentRequest(item.ModName, item.SourceUrl, item.ArchivePath, _settings.ReadyOrNotDirectory));
                    item.InstallId = record.InstallId;
                    item.Status = "Deployed";
                }
                catch (InvalidDataException ex)
                {
                    item.Status = "Import archive manually";
                    SetStatus(ex.Message);
                }
            }

            SetStatus("Deployment complete.");
            return Task.CompletedTask;
        });
    }

    private void UninstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var manager = CreateDeploymentManager();
        var manifest = CreateManifestStore().Load();

        foreach (var item in GetSelectedItems())
        {
            var installId = string.IsNullOrWhiteSpace(item.InstallId)
                ? manifest.Records.FirstOrDefault(record => record.SourceUrl.Equals(item.SourceUrl, StringComparison.OrdinalIgnoreCase))?.InstallId
                : item.InstallId;

            if (string.IsNullOrWhiteSpace(installId))
            {
                item.Status = "No install record";
                continue;
            }

            manager.Uninstall(installId);
            item.InstallId = string.Empty;
            item.Status = "Uninstalled";
        }

        SetStatus("Uninstall complete.");
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _queue.Where(item => item.Status is "Deployed" or "Uninstalled").ToArray())
        {
            _queue.Remove(item);
        }
    }

    private void ClearUserData_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Clear saved API key, selected folders, install manifest, and the current queue? Downloaded archives and deployed game files will not be deleted.",
            "Ready or Not Mod Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settingsStore.Clear();
        var manifestPath = Path.Combine(_appDataDirectory, "install-manifest.json");
        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }

        _queue.Clear();
        _settings = new LocalSettings();
        ApiKeyBox.Clear();
        DownloadDirectoryBox.Text = _settings.DownloadDirectory;
        GameDirectoryBox.Clear();
        ImportDirectoryBox.Text = _settings.ImportDirectory;
        UrlBox.Clear();
        SetStatus("User data cleared.");
    }

    private void SaveSettings()
    {
        _settings = new LocalSettings
        {
            ApiKey = ApiKeyBox.Password.Trim(),
            DownloadDirectory = DownloadDirectoryBox.Text.Trim(),
            ReadyOrNotDirectory = GameDirectoryBox.Text.Trim(),
            ImportDirectory = ImportDirectoryBox.Text.Trim()
        };
        _settingsStore.Save(_settings);
    }

    private void AddQueueItem(NexusModFile file)
    {
        if (_queue.Any(item => item.ModId == file.ModId && item.FileId == file.FileId))
        {
            return;
        }

        _queue.Add(new ModQueueItem
        {
            ModId = file.ModId,
            FileId = file.FileId,
            ModName = file.Name,
            Version = file.Version,
            SourceUrl = file.SourceUrl,
            Status = "Queued"
        });
    }

    private IReadOnlyList<ModQueueItem> GetSelectedItems(bool requireSelection = false)
    {
        var selected = QueueGrid.SelectedItems.Cast<ModQueueItem>().ToArray();
        if (selected.Length > 0)
        {
            return selected;
        }

        return requireSelection ? [] : _queue.ToArray();
    }

    private ModQueueItem? GetSingleSelectedItem()
    {
        return QueueGrid.SelectedItems.Cast<ModQueueItem>().FirstOrDefault();
    }

    private ModQueueItem CreateImportedQueueItem(string archivePath)
    {
        var item = new ModQueueItem
        {
            ModName = Path.GetFileNameWithoutExtension(archivePath),
            Version = "Manual",
            SourceUrl = string.Empty,
            Status = "Imported archive"
        };
        _queue.Add(item);
        return item;
    }

    private DeploymentManager CreateDeploymentManager()
    {
        return new DeploymentManager(CreateManifestStore());
    }

    private InstallManifestStore CreateManifestStore()
    {
        return new InstallManifestStore(Path.Combine(_appDataDirectory, "install-manifest.json"));
    }

    private static bool TryChooseFolder(string currentPath, out string selectedPath)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(currentPath) ? currentPath : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            UseDescriptionForTitle = true,
            Description = "Choose folder"
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            selectedPath = dialog.SelectedPath;
            return true;
        }

        selectedPath = string.Empty;
        return false;
    }

    private static string BuildArchiveFileName(ModQueueItem item)
    {
        var version = string.IsNullOrWhiteSpace(item.Version) ? "latest" : item.Version;
        return $"{item.ModName}-{item.ModId}-{item.FileId}-{version}.zip";
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url.Trim()) { UseShellExecute = true });
    }

    private static void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo(path.Trim()) { UseShellExecute = true });
    }

    private async Task RunUiTaskAsync(Func<Task> action)
    {
        try
        {
            IsEnabled = false;
            SetStatus("Working...");
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "Ready or Not Mod Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }
}
