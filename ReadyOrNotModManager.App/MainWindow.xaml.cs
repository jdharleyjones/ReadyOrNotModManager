using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using ReadyOrNotModManager.Core.Archives;
using ReadyOrNotModManager.Core.Deployment;
using ReadyOrNotModManager.Core.Diagnostics;
using ReadyOrNotModManager.Core.Downloads;
using ReadyOrNotModManager.Core.Manifest;
using ReadyOrNotModManager.Core.Nexus;
using ReadyOrNotModManager.Core.Profiles;
using ReadyOrNotModManager.App.Services;
using Forms = System.Windows.Forms;

namespace ReadyOrNotModManager.App;

public partial class MainWindow : Window
{
    private enum RailStatusState
    {
        Unknown,
        Connected,
        Disconnected
    }

    private const string NexusApiKeyPage = "https://www.nexusmods.com/users/myaccount?tab=api%20access";
    private readonly ObservableCollection<ModQueueItem> _queue = [];
    private readonly ObservableCollection<InstalledModRecord> _installedMods = [];
    private readonly ObservableCollection<ModProfile> _profiles = [];
    private readonly ObservableCollection<ErrorLogEntry> _errors = [];
    private readonly LocalSettingsStore _settingsStore;
    private readonly string _appDataDirectory;
    private LocalSettings _settings = new();
    private string _lastNexusStatus = "Not tested";
    private bool _loadingSettings;
    private bool _hasCheckedForUpdates;

    public MainWindow()
    {
        InitializeComponent();
        _appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadyOrNotModManager");
        _settingsStore = new LocalSettingsStore(_appDataDirectory);
        QueueGrid.ItemsSource = _queue;
        InstalledModsGrid.ItemsSource = _installedMods;
        ProfilesGrid.ItemsSource = _profiles;
        ErrorsGrid.ItemsSource = _errors;
        ThemeSelector.ItemsSource = ThemeManager.Themes;
        LoadSettings();
        RefreshShellData();
        ShowInitialView();
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        try
        {
            _settings = _settingsStore.Load();
            ApiKeyBox.Password = _settings.ApiKey;
            DownloadDirectoryBox.Text = _settings.DownloadDirectory;
            GameDirectoryBox.Text = _settings.ReadyOrNotDirectory;
            ImportDirectoryBox.Text = _settings.ImportDirectory;
            ProfileLibraryDirectoryBox.Text = _settings.ProfileLibraryDirectory;
            AdvancedOptionsBox.IsChecked = _settings.AdvancedOptionsEnabled;
            AutoTestNexusOnLaunchBox.IsChecked = _settings.AutoTestNexusOnLaunch;
            ThemeSelector.SelectedValue = ThemeManager.ResolveThemeName(_settings.ThemeName);
            ThemeManager.ApplyTheme(Resources, _settings.ThemeName);
            SetupApiKeyBox.Password = _settings.ApiKey;
            SetupDownloadDirectoryBox.Text = _settings.DownloadDirectory;
            SetupGameDirectoryBox.Text = _settings.ReadyOrNotDirectory;
            SetupImportDirectoryBox.Text = _settings.ImportDirectory;
            SetupProfileLibraryDirectoryBox.Text = _settings.ProfileLibraryDirectory;
            ValidateSetupFields();
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void ShowInitialView()
    {
        if (SetupGate.ShouldShowSetup(_settings))
        {
            ShellRoot.Visibility = Visibility.Collapsed;
            SetupRoot.Visibility = Visibility.Visible;
            ValidateSetupFields();
            return;
        }

        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Visible;
        ShowPage(DashboardPage, "Dashboard", "Ready or Not mod deployment overview");
        _ = CheckForUpdatesAsync();
        _ = AutoTestNexusConnectionOnLaunchAsync();
    }

    private void RefreshShellData()
    {
        RefreshInstalledMods();
        RefreshProfiles();
        RefreshErrors();
        RefreshDashboard();
    }

    private void RefreshInstalledMods()
    {
        _installedMods.Clear();
        foreach (var record in CreateManifestStore().Load().Records.OrderByDescending(record => record.InstalledAtUtc))
        {
            _installedMods.Add(record);
        }
    }

    private void RefreshProfiles()
    {
        _profiles.Clear();
        Directory.CreateDirectory(_settings.ProfileLibraryDirectory);
        foreach (var profile in CreateProfileStore().LoadAll())
        {
            _profiles.Add(profile);
        }
    }

    private void RefreshErrors()
    {
        _errors.Clear();
        foreach (var entry in CreateErrorLogStore().Load().Entries)
        {
            _errors.Add(entry);
        }
    }

    private void RefreshDashboard()
    {
        var gameOk = ReadyOrNotPaths.LooksLikeInstallDirectory(_settings.ReadyOrNotDirectory);
        var gameStatus = gameOk ? "Detected" : "Not detected";
        RailGameStatusText.Text = gameStatus;
        SetRailStatusIcon(RailGameStatusIcon, gameOk ? RailStatusState.Connected : RailStatusState.Disconnected);
        DashboardGameStatusText.Text = gameStatus;
        var nexusStatus = string.IsNullOrWhiteSpace(_settings.ApiKey) ? "Missing key" : _lastNexusStatus;
        RailNexusStatusText.Text = nexusStatus;
        SetRailStatusIcon(RailNexusStatusIcon, GetNexusRailStatus(nexusStatus));
        DashboardNexusStatusText.Text = nexusStatus;
        DashboardUpdateStatusText.Text = string.IsNullOrWhiteSpace(DashboardUpdateStatusText.Text)
            ? $"Current: v{GetCurrentVersion()}"
            : DashboardUpdateStatusText.Text;

        var summary = DashboardSummaryFactory.Create(CreateManifestStore().Load(), _queue, CreateErrorLogStore().Load(), CreateActivityLogStore().Load());
        InstalledModCountText.Text = summary.InstalledModCount.ToString();
        PendingQueueCountText.Text = summary.PendingQueueCount.ToString();
        RecentActivityList.ItemsSource = summary.RecentActivity.Count == 0
            ? [new RecentActivityItem(DateTimeOffset.UtcNow, "No recent activity yet")]
            : summary.RecentActivity;
    }

    private void SetRailStatusIcon(PackIconMaterial icon, RailStatusState status)
    {
        icon.Kind = status switch
        {
            RailStatusState.Connected => PackIconMaterialKind.CheckCircleOutline,
            RailStatusState.Disconnected => PackIconMaterialKind.CloseCircleOutline,
            _ => PackIconMaterialKind.HelpCircleOutline
        };
        icon.Foreground = status switch
        {
            RailStatusState.Connected => (System.Windows.Media.Brush)FindResource("SuccessBrush"),
            RailStatusState.Disconnected => (System.Windows.Media.Brush)FindResource("DangerBrush"),
            _ => (System.Windows.Media.Brush)FindResource("AccentBrush")
        };
    }

    private static RailStatusState GetNexusRailStatus(string status)
    {
        if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
        {
            return RailStatusState.Connected;
        }

        return status is "Not tested" ? RailStatusState.Unknown : RailStatusState.Disconnected;
    }

    private void ShowPage(FrameworkElement page, string title, string subtitle)
    {
        foreach (var view in new FrameworkElement[] { DashboardPage, ModsPage, QueuePage, ModpacksPage, DownloadsPage, SettingsPage, LogsPage })
        {
            view.Visibility = Visibility.Collapsed;
            view.Opacity = 0;
        }

        page.Visibility = Visibility.Visible;
        page.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;
        RefreshShellData();
    }

    private void DashboardNav_Click(object sender, RoutedEventArgs e) => ShowPage(DashboardPage, "Dashboard", "Ready or Not mod deployment overview");

    private void ModsNav_Click(object sender, RoutedEventArgs e) => ShowPage(ModsPage, "Mods", "Installed files tracked by the local manifest");

    private void QueueNav_Click(object sender, RoutedEventArgs e) => ShowPage(QueuePage, "Queue", "Download and deploy selected Nexus files");

    private void ModpacksNav_Click(object sender, RoutedEventArgs e) => ShowPage(ModpacksPage, "Modpacks", "Save and switch local mod profiles");

    private void DownloadsNav_Click(object sender, RoutedEventArgs e) => ShowPage(DownloadsPage, "Downloads", "Archive storage and browser fallback imports");

    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, "Settings", "Connection, folders, and advanced options");

    private void LogsNav_Click(object sender, RoutedEventArgs e) => ShowPage(LogsPage, "Logs/Errors", "Download and deployment failures");

    private async Task CheckForUpdatesAsync()
    {
        if (_hasCheckedForUpdates)
        {
            return;
        }

        _hasCheckedForUpdates = true;
        DashboardUpdateStatusText.Text = $"Current: v{GetCurrentVersion()} | Checking...";
        using var http = new HttpClient();
        var result = await new AppUpdateChecker(http).CheckLatestAsync(GetCurrentVersion(), CancellationToken.None);
        DashboardUpdateStatusText.Text = result.Status switch
        {
            AppUpdateStatus.UpToDate => $"Current: v{GetCurrentVersion()} | Up to date",
            AppUpdateStatus.UpdateAvailable => $"Current: v{GetCurrentVersion()} | {result.Message}",
            _ => $"Current: v{GetCurrentVersion()} | Unable to check"
        };
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        SetStatus("Settings saved.");
    }

    private async Task AutoTestNexusConnectionOnLaunchAsync()
    {
        if (!_settings.AutoTestNexusOnLaunch || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return;
        }

        SetStatus("Testing Nexus connection...", logActivity: false);
        await ValidateNexusConnectionAsync(_settings.ApiKey);
    }

    private void ThemeSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || ThemeSelector.SelectedValue is not string themeName)
        {
            return;
        }

        ThemeManager.ApplyTheme(Resources, themeName);
        SaveSettings();
        SetStatus($"Theme changed: {ThemeManager.GetTheme(themeName).DisplayName}");
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

    private void BrowseSetupDownloadDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(SetupDownloadDirectoryBox.Text, out var selected))
        {
            SetupDownloadDirectoryBox.Text = selected;
            ValidateSetupFields();
        }
    }

    private void BrowseSetupGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(SetupGameDirectoryBox.Text, out var selected))
        {
            SetupGameDirectoryBox.Text = selected;
            ValidateSetupFields();
        }
    }

    private void BrowseSetupImportDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(SetupImportDirectoryBox.Text, out var selected))
        {
            SetupImportDirectoryBox.Text = selected;
            ValidateSetupFields();
        }
    }

    private void BrowseSetupProfileLibraryDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(SetupProfileLibraryDirectoryBox.Text, out var selected))
        {
            SetupProfileLibraryDirectoryBox.Text = selected;
            ValidateSetupFields();
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            var apiKey = SetupRoot.Visibility == Visibility.Visible ? SetupApiKeyBox.Password.Trim() : ApiKeyBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _lastNexusStatus = "Missing key";
                SetStatus("Enter a Nexus API key before testing.");
                ValidateSetupFields();
                return;
            }

            await ValidateNexusConnectionAsync(apiKey);
        });
    }

    private async Task ValidateNexusConnectionAsync(string apiKey)
    {
        using var http = new HttpClient();
        var result = await new NexusClient(http, apiKey).ValidateApiKeyAsync(CancellationToken.None);
        _lastNexusStatus = result.IsValid
            ? string.IsNullOrWhiteSpace(result.UserName) ? "Connected" : $"Connected: {result.UserName}"
            : "Connection failed";
        SetStatus(result.Message);
        ValidateSetupFields();
        RefreshDashboard();
    }

    private void AutoDetectGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var detected = ReadyOrNotInstallDetector.FindInstallDirectory();
        if (string.IsNullOrWhiteSpace(detected))
        {
            SetStatus("Ready or Not was not found in the usual Steam library folders.");
            SetupGameStatusText.Text = "Auto-detect could not find Ready or Not";
            SetupGameStatusText.Foreground = FindBrush("DangerBrush");
            return;
        }

        SetupGameDirectoryBox.Text = detected;
        GameDirectoryBox.Text = detected;
        SetStatus("Ready or Not folder detected.");
        ValidateSetupFields();
    }

    private void ContinueSetup_Click(object sender, RoutedEventArgs e)
    {
        ValidateSetupFields();
        if (string.IsNullOrWhiteSpace(SetupApiKeyBox.Password) ||
            !ReadyOrNotPaths.LooksLikeInstallDirectory(SetupGameDirectoryBox.Text))
        {
            SetupMessageText.Text = "Add a Nexus API key and a valid Ready or Not folder before continuing.";
            SetupMessageText.Foreground = FindBrush("DangerBrush");
            return;
        }

        CopySetupFieldsToSettingsFields();
        SaveSettings(setupCompleted: true, forceSetupWizard: false);
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Visible;
        RefreshShellData();
        ShowPage(DashboardPage, "Dashboard", "Ready or Not mod deployment overview");
        SetStatus("Setup complete.");
    }

    private void ResetSetupWizard_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings(setupCompleted: false, forceSetupWizard: true);
        LoadSettings();
        ShellRoot.Visibility = Visibility.Collapsed;
        SetupRoot.Visibility = Visibility.Visible;
        SetStatus("Setup wizard reset.");
    }

    private void BrowseProfileLibraryDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (TryChooseFolder(ProfileLibraryDirectoryBox.Text, out var selected))
        {
            ProfileLibraryDirectoryBox.Text = selected;
            SaveSettings();
        }
    }

    private void OpenProfileLibraryDirectory_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Directory.CreateDirectory(_settings.ProfileLibraryDirectory);
        OpenFolder(_settings.ProfileLibraryDirectory);
        SetStatus("Opened modpack library folder.");
    }

    private async void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var input = sender is FrameworkElement { Tag: System.Windows.Controls.TextBox taggedBox }
            ? taggedBox
            : UrlBox;

        await RunUiTaskAsync(async () =>
        {
            SaveSettings();
            var reference = NexusUrlParser.Parse(input.Text);
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

            input.Clear();
            RefreshDashboard();
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
            var items = _queue.Where(item => string.IsNullOrWhiteSpace(item.ArchivePath)).ToArray();
            var total = Math.Max(items.Length, 1);

            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var itemNumber = index + 1;
                SetProgress(index / (double)total, $"Downloading {itemNumber} of {items.Length}");
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
                    var fileProgress = new Progress<double>(value =>
                        SetProgress((index + Math.Clamp(value, 0, 1)) / total, $"Downloading {itemNumber} of {items.Length}"));
                    item.ArchivePath = await downloader.DownloadAsync(
                        downloadUri,
                        _settings.DownloadDirectory,
                        BuildArchiveFileName(item),
                        fileProgress,
                        CancellationToken.None);
                    item.Status = "Downloaded";
                }
                catch (Exception ex) when (ex is NexusApiException or HttpRequestException or InvalidDataException)
                {
                    item.Status = "Failed - see errors";
                    LogItemError("Download", item, ex);
                    OpenUrl(item.SourceUrl);
                }
            }

            SetProgress(1, "Download pass complete");
            RefreshDashboard();
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
            Filter = "Mod archives (*.zip;*.rar;*.7z;*.7zip)|*.zip;*.rar;*.7z;*.7zip|All files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(_settings.ImportDirectory) ? _settings.ImportDirectory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog(this) == true)
        {
            var imported = ArchiveImportPlanner.ImportArchives(_queue, item, dialog.FileNames, _settings.ActiveProfileId);
            var lastArchivePath = imported.LastOrDefault()?.ArchivePath ?? dialog.FileName;
            ImportDirectoryBox.Text = Path.GetDirectoryName(lastArchivePath) ?? ImportDirectoryBox.Text;
            SaveSettings();
            QueueGrid.SelectedItem = imported.LastOrDefault();
            RefreshDashboard();
            SetStatus($"Imported {imported.Count} archive file(s).");
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

    private void RemoveSelectedQueueItems_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems(requireSelection: true);
        if (items.Count == 0)
        {
            SetStatus("Select queue items before removing them.");
            return;
        }

        var removed = QueueDeploymentPlanner.RemoveSelectedItems(_queue, items);
        RefreshDashboard();
        SetStatus($"Removed {removed.Count} queue item(s).");
    }

    private async void DeploySelected_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            SaveSettings();
            await DeployQueueItemsAsync(GetSelectedItems().ToArray(), "Deployment complete.");
        });
    }

    private async void DeployDownloaded_Click(object sender, RoutedEventArgs e)
    {
        await RunUiTaskAsync(async () =>
        {
            SaveSettings();
            var deployable = QueueDeploymentPlanner.GetDeployableDownloadedItems(_queue);
            if (deployable.Count == 0)
            {
                SetStatus("No downloaded queue items are ready to deploy.");
                return;
            }

            await DeployQueueItemsAsync(deployable, $"Deployed {deployable.Count} downloaded queue item(s).");
        });
    }

    private async Task DeployQueueItemsAsync(IReadOnlyList<ModQueueItem> items, string completionMessage)
    {
        if (!ReadyOrNotPaths.LooksLikeInstallDirectory(_settings.ReadyOrNotDirectory))
        {
            throw new DirectoryNotFoundException("Choose the Ready or Not install folder that contains ReadyOrNot\\Content\\Paks.");
        }

        var manager = CreateDeploymentManager();
        var total = Math.Max(items.Count, 1);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemNumber = index + 1;
            SetProgress(index / (double)total, $"Deploying {itemNumber} of {items.Count}");
            if (!File.Exists(item.ArchivePath))
            {
                item.Status = "Missing zip";
                LogItemError("Deploy", item, new FileNotFoundException("The downloaded archive could not be found.", item.ArchivePath));
                continue;
            }

            try
            {
                var selectedEntries = ResolveSelectedArchiveEntries(item);
                var itemIndex = index;
                var deployProgress = new Progress<double>(value =>
                    SetProgress((itemIndex + Math.Clamp(value, 0, 1)) / total, $"Deploying {itemNumber} of {items.Count}"));
                var request = new DeploymentRequest(
                    ModName: item.ModName,
                    SourceUrl: item.SourceUrl,
                    ArchivePath: item.ArchivePath,
                    ReadyOrNotInstallDirectory: _settings.ReadyOrNotDirectory,
                    ProfileId: item.ProfileId,
                    ModId: item.ModId,
                    FileId: item.FileId,
                    ExistingInstallId: item.InstallId,
                    SelectedArchiveEntries: selectedEntries.Count == 0 ? null : selectedEntries,
                    Progress: deployProgress);
                var record = await Task.Run(() => manager.Deploy(request));
                item.InstallId = record.InstallId;
                item.SelectedArchiveEntries = record.SelectedArchiveEntries.ToList();
                item.Status = "Deployed";
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                item.Status = "Failed - see errors";
                LogItemError("Deploy", item, ex);
                SetStatus(ex.Message);
            }

            await Task.Yield();
        }

        SetProgress(1, "Deployment complete");
        RefreshShellData();
        SetStatus(completionMessage);
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
        RefreshShellData();
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _queue.Where(item => item.Status is "Deployed" or "Uninstalled").ToArray())
        {
            _queue.Remove(item);
        }

        RefreshDashboard();
    }

    private void OpenProfiles_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Directory.CreateDirectory(_settings.ProfileLibraryDirectory);
        var window = new ProfilesWindow(
            CreateProfileStore(),
            () => _queue.ToArray(),
            LoadProfileIntoQueue,
            ActivateProfileAsync)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OpenErrors_Click(object sender, RoutedEventArgs e)
    {
        var window = new ErrorsWindow(CreateErrorLogStore())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var paks = ReadyOrNotPaths.GetPaksDirectory(_settings.ReadyOrNotDirectory);
        if (Directory.Exists(paks))
        {
            OpenFolder(paks);
            return;
        }

        SetStatus("Ready or Not Paks folder was not found.");
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        if (Directory.Exists(_settings.ReadyOrNotDirectory))
        {
            OpenFolder(_settings.ReadyOrNotDirectory);
            return;
        }

        SetStatus("Ready or Not install folder was not found.");
    }

    private void RunGame_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var target = ReadyOrNotLauncher.Resolve(_settings.ReadyOrNotDirectory, preferSteam: true);
        try
        {
            LaunchTarget(target);
            SetStatus(target.Message);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            var fallback = ReadyOrNotLauncher.Resolve(_settings.ReadyOrNotDirectory, preferSteam: false);
            if (!fallback.CanLaunch)
            {
                SetStatus(fallback.Message);
                return;
            }

            LaunchTarget(fallback);
            SetStatus(fallback.Message);
        }
    }

    private void UninstallInstalledSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = InstalledModsGrid.SelectedItems.Cast<InstalledModRecord>().ToArray();
        if (selected.Length == 0)
        {
            SetStatus("Select installed mods before uninstalling.");
            return;
        }

        var manager = CreateDeploymentManager();
        foreach (var record in selected)
        {
            manager.Uninstall(record.InstallId);
        }

        RefreshShellData();
        SetStatus("Selected installed mods uninstalled.");
    }

    private void SaveProfileNew_Click(object sender, RoutedEventArgs e)
    {
        var profile = CreateProfileFromQueue(new ModProfile
        {
            Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text)
                ? $"Modpack {DateTime.Now:yyyy-MM-dd HH-mm}"
                : ProfileNameBox.Text.Trim()
        });
        CreateProfileStore().Save(profile, copyArchives: true);
        RefreshProfiles();
        ProfilesGrid.SelectedItem = _profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId);
        SetStatus($"Saved modpack: {profile.Name}");
    }

    private void UpdateProfileSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ProfileNameBox.Text))
        {
            profile.Name = ProfileNameBox.Text.Trim();
        }

        CreateProfileStore().Save(CreateProfileFromQueue(profile), copyArchives: true);
        RefreshProfiles();
        SetStatus($"Updated modpack: {profile.Name}");
    }

    private void LoadProfileSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        LoadProfileIntoQueue(profile);
        ShowPage(QueuePage, "Queue", "Download and deploy selected Nexus files");
    }

    private async void ActivateProfileSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        await RunUiTaskAsync(async () =>
        {
            await ActivateProfileAsync(profile);
            RefreshShellData();
        });
    }

    private void DeleteProfileSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"Delete the local profile \"{profile.Name}\"? This removes copied profile archives, but does not uninstall active game files.",
            "Ready or Not Mod Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        CreateProfileStore().Delete(profile.ProfileId);
        RefreshProfiles();
        SetStatus($"Deleted modpack: {profile.Name}");
    }

    private void ProfilesGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row is not null)
        {
            row.IsSelected = true;
            ProfilesGrid.SelectedItem = row.Item;
        }
    }

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        var oldName = profile.Name;
        var requestedName = string.IsNullOrWhiteSpace(ProfileNameBox.Text)
            ? ShowRenamePrompt(oldName)
            : ProfileNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            SetStatus("Rename canceled.");
            return;
        }

        var result = CreateProfileStore().Rename(profile.ProfileId, requestedName);
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        RefreshProfiles();
        ProfilesGrid.SelectedItem = _profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId);
        ProfileNameBox.Clear();
        SetStatus($"Renamed modpack: {oldName} -> {requestedName.Trim()}");
    }

    private void OpenErrorNexus_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedError();
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.SourceUrl))
        {
            OpenUrl(entry.SourceUrl);
        }
    }

    private void OpenErrorGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedError();
        if (entry is null)
        {
            return;
        }

        var paks = ReadyOrNotPaths.GetPaksDirectory(entry.ReadyOrNotDirectory);
        if (Directory.Exists(paks))
        {
            OpenFolder(paks);
        }
        else if (Directory.Exists(entry.ReadyOrNotDirectory))
        {
            OpenFolder(entry.ReadyOrNotDirectory);
        }
    }

    private void OpenErrorArchiveFolder_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedError();
        var directory = entry is null ? null : Path.GetDirectoryName(entry.ArchivePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            OpenFolder(directory);
        }
    }

    private void CopyErrorManualFix_Click(object sender, RoutedEventArgs e)
    {
        var entry = GetSelectedError();
        if (entry is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText($"""
            {entry.ModName} could not be {entry.Operation.ToLowerInvariant()} automatically.

            Reason: {entry.Message}

            You may need to download this mod manually from Nexus Mods, then use Import archive in the manager or manually place the mod's .pak/.ucas/.utoc files into the Ready or Not Paks folder.

            Nexus page: {entry.SourceUrl}
            Archive: {entry.ArchivePath}
            Ready or Not folder: {entry.ReadyOrNotDirectory}
            """);
        SetStatus("Manual fix note copied.");
    }

    private void ClearErrors_Click(object sender, RoutedEventArgs e)
    {
        CreateErrorLogStore().Clear();
        RefreshErrors();
        RefreshDashboard();
        SetStatus("Errors cleared.");
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
        CreateErrorLogStore().Clear();
        CreateActivityLogStore().Clear();

        _queue.Clear();
        _settings = new LocalSettings();
        ApiKeyBox.Clear();
        DownloadDirectoryBox.Text = _settings.DownloadDirectory;
        GameDirectoryBox.Clear();
        ImportDirectoryBox.Text = _settings.ImportDirectory;
        ProfileLibraryDirectoryBox.Text = _settings.ProfileLibraryDirectory;
        AdvancedOptionsBox.IsChecked = false;
        AutoTestNexusOnLaunchBox.IsChecked = false;
        UrlBox.Clear();
        SetProgress(0, string.Empty);
        LoadSettings();
        ShellRoot.Visibility = Visibility.Collapsed;
        SetupRoot.Visibility = Visibility.Visible;
        SetStatus("User data cleared.", logActivity: false);
    }

    private void SaveSettings(bool? setupCompleted = null, bool? forceSetupWizard = null)
    {
        var defaults = new LocalSettings();
        _settings = new LocalSettings
        {
            ApiKey = ApiKeyBox.Password.Trim(),
            DownloadDirectory = string.IsNullOrWhiteSpace(DownloadDirectoryBox.Text) ? defaults.DownloadDirectory : DownloadDirectoryBox.Text.Trim(),
            ReadyOrNotDirectory = GameDirectoryBox.Text.Trim(),
            ImportDirectory = string.IsNullOrWhiteSpace(ImportDirectoryBox.Text) ? defaults.ImportDirectory : ImportDirectoryBox.Text.Trim(),
            ProfileLibraryDirectory = string.IsNullOrWhiteSpace(ProfileLibraryDirectoryBox.Text) ? defaults.ProfileLibraryDirectory : ProfileLibraryDirectoryBox.Text.Trim(),
            ActiveProfileId = _settings.ActiveProfileId,
            ThemeName = ThemeSelector.SelectedValue as string ?? _settings.ThemeName,
            AdvancedOptionsEnabled = AdvancedOptionsBox.IsChecked == true,
            AutoTestNexusOnLaunch = AutoTestNexusOnLaunchBox.IsChecked == true,
            SetupCompleted = setupCompleted ?? _settings.SetupCompleted,
            ForceSetupWizard = forceSetupWizard ?? _settings.ForceSetupWizard
        };
        _settingsStore.Save(_settings);
        ValidateSetupFields();
        RefreshDashboard();
    }

    private void CopySetupFieldsToSettingsFields()
    {
        ApiKeyBox.Password = SetupApiKeyBox.Password.Trim();
        GameDirectoryBox.Text = SetupGameDirectoryBox.Text.Trim();
        DownloadDirectoryBox.Text = SetupDownloadDirectoryBox.Text.Trim();
        ImportDirectoryBox.Text = SetupImportDirectoryBox.Text.Trim();
        ProfileLibraryDirectoryBox.Text = SetupProfileLibraryDirectoryBox.Text.Trim();
    }

    private void ValidateSetupFields()
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(SetupApiKeyBox.Password);
        var gameValid = ReadyOrNotPaths.LooksLikeInstallDirectory(SetupGameDirectoryBox.Text);
        var downloadValid = ValidateOrCreateFolderText(SetupDownloadDirectoryBox.Text);
        var importValid = ValidateOrCreateFolderText(SetupImportDirectoryBox.Text);
        var libraryValid = ValidateOrCreateFolderText(SetupProfileLibraryDirectoryBox.Text);

        SetValidation(SetupApiKeyBox, SetupNexusStatusText, hasApiKey, hasApiKey ? _lastNexusStatus : "Nexus API key is required");
        SetValidation(SetupGameDirectoryBox, SetupGameStatusText, gameValid, gameValid ? "Ready or Not folder validated" : "Choose the folder that contains ReadyOrNot\\Content\\Paks");
        var foldersOk = downloadValid && importValid && libraryValid;
        SetValidation(SetupDownloadDirectoryBox, SetupFoldersStatusText, foldersOk, foldersOk ? "Storage folders are ready" : "Choose valid storage folders");
        SetValidation(SetupImportDirectoryBox, null, importValid, string.Empty);
        SetValidation(SetupProfileLibraryDirectoryBox, null, libraryValid, string.Empty);
    }

    private static bool ValidateOrCreateFolderText(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(path.Trim());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void SetValidation(System.Windows.Controls.Control control, TextBlock? statusText, bool isValid, string message)
    {
        control.BorderBrush = isValid ? FindBrush("SuccessBrush") : FindBrush("DangerBrush");
        if (statusText is not null)
        {
            statusText.Text = message;
            statusText.Foreground = isValid ? FindBrush("SuccessBrush") : FindBrush("DangerBrush");
        }
    }

    private System.Windows.Media.Brush FindBrush(string key)
    {
        return (System.Windows.Media.Brush)FindResource(key);
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
            ProfileId = _settings.ActiveProfileId,
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

    private IReadOnlyList<string> ResolveSelectedArchiveEntries(ModQueueItem item)
    {
        if (item.SelectedArchiveEntries.Count > 0)
        {
            return item.SelectedArchiveEntries;
        }

        if (_settings.AdvancedOptionsEnabled)
        {
            var groups = ArchiveScanner.GetDeployableGroups(item.ArchivePath);
            if (groups.Count > 1)
            {
                var dialog = new ArchiveSelectionWindow(groups)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() == true)
                {
                    item.SelectedArchiveEntries = dialog.SelectedEntryPaths.ToList();
                    return item.SelectedArchiveEntries;
                }

                throw new OperationCanceledException("Deployment was canceled.");
            }
        }

        return [];
    }

    private void LoadProfileIntoQueue(ModProfile profile)
    {
        _queue.Clear();
        foreach (var profileItem in profile.Items)
        {
            _queue.Add(CreateQueueItem(profile, profileItem));
        }

        SetStatus($"Loaded modpack: {profile.Name}");
    }

    private async Task ActivateProfileAsync(ModProfile profile)
    {
        SaveSettings();
        if (!ReadyOrNotPaths.LooksLikeInstallDirectory(_settings.ReadyOrNotDirectory))
        {
            throw new DirectoryNotFoundException("Choose the Ready or Not install folder that contains ReadyOrNot\\Content\\Paks.");
        }

        var manager = CreateDeploymentManager();
        if (!string.IsNullOrWhiteSpace(_settings.ActiveProfileId))
        {
            manager.UninstallProfile(_settings.ActiveProfileId);
        }

        LoadProfileIntoQueue(profile);
        var total = Math.Max(_queue.Count, 1);
        for (var index = 0; index < _queue.Count; index++)
        {
            var item = _queue[index];
            SetProgress(index / (double)total, $"Activating {index + 1} of {_queue.Count}");
            if (!File.Exists(item.ArchivePath))
            {
                item.Status = "Missing archive";
                LogItemError("Activate profile", item, new FileNotFoundException("The profile archive could not be found.", item.ArchivePath));
                continue;
            }

            try
            {
                var itemIndex = index;
                var deployProgress = new Progress<double>(value =>
                    SetProgress((itemIndex + Math.Clamp(value, 0, 1)) / total, $"Activating {itemIndex + 1} of {_queue.Count}"));
                var request = new DeploymentRequest(
                    ModName: item.ModName,
                    SourceUrl: item.SourceUrl,
                    ArchivePath: item.ArchivePath,
                    ReadyOrNotInstallDirectory: _settings.ReadyOrNotDirectory,
                    ProfileId: profile.ProfileId,
                    ModId: item.ModId,
                    FileId: item.FileId,
                    ExistingInstallId: item.InstallId,
                    SelectedArchiveEntries: item.SelectedArchiveEntries.Count == 0 ? null : item.SelectedArchiveEntries,
                    Progress: deployProgress);
                var record = await Task.Run(() => manager.Deploy(request));
                item.InstallId = record.InstallId;
                item.Status = "Deployed";
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                item.Status = "Failed - see errors";
                LogItemError("Activate profile", item, ex);
            }

            await Task.Yield();
        }

        _settings.ActiveProfileId = profile.ProfileId;
        SaveSettings();
        CreateProfileStore().Save(CreateProfileFromQueue(profile), copyArchives: false);
        SetProgress(1, "Profile activated");
        SetStatus($"Activated modpack: {profile.Name}");
    }

    private ModProfile CreateProfileFromQueue(ModProfile profile)
    {
        profile.Items = _queue.Select(item => new ModProfileItem
        {
            ModId = item.ModId,
            FileId = item.FileId,
            ModName = item.ModName,
            Version = item.Version,
            SourceUrl = item.SourceUrl,
            ArchivePath = item.ArchivePath,
            SelectedArchiveEntries = item.SelectedArchiveEntries.ToList(),
            LastInstallId = item.InstallId
        }).ToList();
        return profile;
    }

    private static ModQueueItem CreateQueueItem(ModProfile profile, ModProfileItem profileItem)
    {
        return new ModQueueItem
        {
            ModId = profileItem.ModId,
            FileId = profileItem.FileId,
            ModName = profileItem.ModName,
            Version = profileItem.Version,
            SourceUrl = profileItem.SourceUrl,
            ArchivePath = profileItem.ArchivePath,
            InstallId = profileItem.LastInstallId,
            ProfileId = profile.ProfileId,
            SelectedArchiveEntries = profileItem.SelectedArchiveEntries.ToList(),
            Status = File.Exists(profileItem.ArchivePath) ? "Loaded from profile" : "Missing archive"
        };
    }

    private ModProfile? GetSelectedProfile()
    {
        var profile = ProfilesGrid.SelectedItem as ModProfile;
        if (profile is null)
        {
            SetStatus("Select a modpack first.");
        }

        return profile;
    }

    private ErrorLogEntry? GetSelectedError()
    {
        var entry = ErrorsGrid.SelectedItem as ErrorLogEntry;
        if (entry is null)
        {
            SetStatus("Select an error first.");
        }

        return entry;
    }

    private DeploymentManager CreateDeploymentManager()
    {
        return new DeploymentManager(CreateManifestStore());
    }

    private InstallManifestStore CreateManifestStore()
    {
        return new InstallManifestStore(Path.Combine(_appDataDirectory, "install-manifest.json"));
    }

    private ErrorLogStore CreateErrorLogStore()
    {
        return new ErrorLogStore(Path.Combine(_appDataDirectory, "error-log.json"));
    }

    private ActivityLogStore CreateActivityLogStore()
    {
        return new ActivityLogStore(Path.Combine(_appDataDirectory, "activity-log.json"));
    }

    private ModProfileStore CreateProfileStore()
    {
        return new ModProfileStore(_settings.ProfileLibraryDirectory);
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

    private string ShowRenamePrompt(string currentName)
    {
        var dialog = new Window
        {
            Owner = this,
            Title = "Rename modpack",
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("PanelBrush")
        };
        var input = new System.Windows.Controls.TextBox
        {
            Text = currentName,
            Margin = new Thickness(0, 8, 0, 12)
        };
        var result = string.Empty;
        var saveButton = new System.Windows.Controls.Button
        {
            Content = "Rename",
            Style = (Style)FindResource("SpecialButton")
        };
        saveButton.Click += (_, _) =>
        {
            result = input.Text.Trim();
            dialog.DialogResult = true;
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock { Text = "Modpack name", FontWeight = FontWeights.SemiBold },
                input,
                new WrapPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Children = { saveButton }
                }
            }
        };
        input.SelectAll();
        return dialog.ShowDialog() == true ? result : string.Empty;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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

    private static void LaunchTarget(ReadyOrNotLaunchTarget target)
    {
        if (!target.CanLaunch)
        {
            throw new InvalidOperationException(target.Message);
        }

        var info = new ProcessStartInfo(target.Target)
        {
            UseShellExecute = target.UseShellExecute
        };
        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
        {
            info.WorkingDirectory = target.WorkingDirectory;
        }

        Process.Start(info);
    }

    private async Task RunUiTaskAsync(Func<Task> action)
    {
        try
        {
            IsEnabled = false;
            SetStatus("Working...", logActivity: false);
            SetProgress(0, string.Empty);
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

    private void SetStatus(string message, bool logActivity = true)
    {
        StatusText.Text = message;
        if (logActivity && !string.IsNullOrWhiteSpace(message))
        {
            CreateActivityLogStore().Append(message);
            if (ShellRoot.Visibility == Visibility.Visible)
            {
                RefreshDashboard();
            }
        }
    }

    private void SetProgress(double value, string message)
    {
        ProgressPanel.Visibility = string.IsNullOrWhiteSpace(message) && value <= 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        OverallProgressBar.Value = Math.Clamp(value, 0, 1) * 100;
        ProgressText.Text = message;
    }

    private void LogItemError(string operation, ModQueueItem item, Exception exception)
    {
        CreateErrorLogStore().Append(new ErrorLogEntry
        {
            Operation = operation,
            ModName = item.ModName,
            ModId = item.ModId,
            FileId = item.FileId,
            SourceUrl = item.SourceUrl,
            ArchivePath = item.ArchivePath,
            ReadyOrNotDirectory = _settings.ReadyOrNotDirectory,
            Message = exception.Message,
            Detail = exception.ToString()
        });
        SetStatus($"{operation} failed for {item.ModName}: {exception.Message}");
    }
}
