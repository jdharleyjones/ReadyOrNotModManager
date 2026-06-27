using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReadyOrNotModManager.App;

public sealed class LocalSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ReadyOrNotMods");
    public string ReadyOrNotDirectory { get; set; } = string.Empty;
    public string ImportDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string ProfileLibraryDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ReadyOrNotModpacks");
    public string ActiveProfileId { get; set; } = string.Empty;
    public bool AdvancedOptionsEnabled { get; set; }
    public bool SetupCompleted { get; set; }
    public bool ForceSetupWizard { get; set; }
}

public sealed class LocalSettingsStore
{
    private readonly string _path;

    public LocalSettingsStore(string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        _path = Path.Combine(appDataDirectory, "settings.json");
    }

    public LocalSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new LocalSettings();
        }

        var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(_path)) ?? new SettingsDto();
        var defaults = new LocalSettings();
        return new LocalSettings
        {
            ApiKey = Unprotect(dto.ProtectedApiKey),
            DownloadDirectory = string.IsNullOrWhiteSpace(dto.DownloadDirectory) ? defaults.DownloadDirectory : dto.DownloadDirectory,
            ReadyOrNotDirectory = dto.ReadyOrNotDirectory ?? string.Empty,
            ImportDirectory = string.IsNullOrWhiteSpace(dto.ImportDirectory) ? defaults.ImportDirectory : dto.ImportDirectory,
            ProfileLibraryDirectory = string.IsNullOrWhiteSpace(dto.ProfileLibraryDirectory) ? defaults.ProfileLibraryDirectory : dto.ProfileLibraryDirectory,
            ActiveProfileId = dto.ActiveProfileId ?? string.Empty,
            AdvancedOptionsEnabled = dto.AdvancedOptionsEnabled,
            SetupCompleted = dto.SetupCompleted,
            ForceSetupWizard = dto.ForceSetupWizard
        };
    }

    public void Save(LocalSettings settings)
    {
        var dto = new SettingsDto
        {
            ProtectedApiKey = Protect(settings.ApiKey),
            DownloadDirectory = settings.DownloadDirectory,
            ReadyOrNotDirectory = settings.ReadyOrNotDirectory,
            ImportDirectory = settings.ImportDirectory,
            ProfileLibraryDirectory = settings.ProfileLibraryDirectory,
            ActiveProfileId = settings.ActiveProfileId,
            AdvancedOptionsEnabled = settings.AdvancedOptionsEnabled,
            SetupCompleted = settings.SetupCompleted,
            ForceSetupWizard = settings.ForceSetupWizard
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private sealed class SettingsDto
    {
        public string? ProtectedApiKey { get; set; }
        public string? DownloadDirectory { get; set; }
        public string? ReadyOrNotDirectory { get; set; }
        public string? ImportDirectory { get; set; }
        public string? ProfileLibraryDirectory { get; set; }
        public string? ActiveProfileId { get; set; }
        public bool AdvancedOptionsEnabled { get; set; }
        public bool SetupCompleted { get; set; }
        public bool ForceSetupWizard { get; set; }
    }
}
