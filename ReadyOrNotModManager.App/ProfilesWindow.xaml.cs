using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ReadyOrNotModManager.Core.Profiles;

namespace ReadyOrNotModManager.App;

public partial class ProfilesWindow : Window
{
    private readonly ModProfileStore _store;
    private readonly Func<IReadOnlyList<ModQueueItem>> _queueProvider;
    private readonly Action<ModProfile> _loadProfile;
    private readonly Func<ModProfile, Task> _activateProfile;
    private readonly ObservableCollection<ModProfile> _profiles = [];

    public ProfilesWindow(
        ModProfileStore store,
        Func<IReadOnlyList<ModQueueItem>> queueProvider,
        Action<ModProfile> loadProfile,
        Func<ModProfile, Task> activateProfile)
    {
        InitializeComponent();
        _store = store;
        _queueProvider = queueProvider;
        _loadProfile = loadProfile;
        _activateProfile = activateProfile;
        ProfilesGrid.ItemsSource = _profiles;
        Reload();
    }

    private void Reload()
    {
        _profiles.Clear();
        foreach (var profile in _store.LoadAll())
        {
            _profiles.Add(profile);
        }
    }

    private void SaveNew_Click(object sender, RoutedEventArgs e)
    {
        var profile = CreateProfileFromQueue(new ModProfile
        {
            Name = GetProfileName()
        });
        _store.Save(profile, copyArchives: true);
        Reload();
        ProfilesGrid.SelectedItem = _profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId);
    }

    private void UpdateSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        profile = CreateProfileFromQueue(profile);
        if (!string.IsNullOrWhiteSpace(ProfileNameBox.Text))
        {
            profile.Name = ProfileNameBox.Text.Trim();
        }

        _store.Save(profile, copyArchives: true);
        Reload();
    }

    private void LoadSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        _loadProfile(profile);
    }

    private async void ActivateSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        try
        {
            IsEnabled = false;
            await _activateProfile(profile);
            Reload();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Ready or Not Mod Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = GetSelectedProfile();
        if (profile is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"Delete the local profile \"{profile.Name}\"? This removes profile metadata and copied profile archives, but does not uninstall active game files.",
            "Ready or Not Mod Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _store.Delete(profile.ProfileId);
        Reload();
    }

    private ModProfile CreateProfileFromQueue(ModProfile profile)
    {
        profile.Items = _queueProvider()
            .Select(item => new ModProfileItem
            {
                ModId = item.ModId,
                FileId = item.FileId,
                ModName = item.ModName,
                Version = item.Version,
                SourceUrl = item.SourceUrl,
                ArchivePath = item.ArchivePath,
                SelectedArchiveEntries = item.SelectedArchiveEntries.ToList(),
                LastInstallId = item.InstallId
            })
            .ToList();
        return profile;
    }

    private string GetProfileName()
    {
        return string.IsNullOrWhiteSpace(ProfileNameBox.Text)
            ? $"Modpack {DateTime.Now:yyyy-MM-dd HH-mm}"
            : ProfileNameBox.Text.Trim();
    }

    private ModProfile? GetSelectedProfile()
    {
        var profile = ProfilesGrid.SelectedItem as ModProfile;
        if (profile is null)
        {
            System.Windows.MessageBox.Show(this, "Select a profile first.", "Ready or Not Mod Manager", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return profile;
    }
}
