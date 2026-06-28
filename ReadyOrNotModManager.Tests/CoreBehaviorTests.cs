using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Media;
using ReadyOrNotModManager.Core.Archives;
using ReadyOrNotModManager.Core.Deployment;
using ReadyOrNotModManager.Core.Manifest;
using ReadyOrNotModManager.Core.Nexus;
using ReadyOrNotModManager.Core.Downloads;
using ReadyOrNotModManager.Core.Diagnostics;
using ReadyOrNotModManager.Core.Profiles;
using ReadyOrNotModManager.App;
using ReadyOrNotModManager.App.Services;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Writers;

namespace ReadyOrNotModManager.Tests;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void NexusUrlParser_ParsesReadyOrNotModUrl()
    {
        var result = NexusUrlParser.Parse("https://www.nexusmods.com/games/readyornot/mods/123?tab=files");

        var mod = Assert.IsType<NexusModReference>(result);
        Assert.Equal(123, mod.ModId);
        Assert.Equal("readyornot", mod.GameDomain);
    }

    [Fact]
    public void NexusUrlParser_ParsesReadyOrNotShorthandModUrl()
    {
        var result = NexusUrlParser.Parse("https://www.nexusmods.com/readyornot/mods/7975 ");

        var mod = Assert.IsType<NexusModReference>(result);
        Assert.Equal(7975, mod.ModId);
        Assert.Equal("readyornot", mod.GameDomain);
        Assert.Equal("https://www.nexusmods.com/readyornot/mods/7975", mod.SourceUrl);
    }

    [Fact]
    public void NexusUrlParser_ParsesReadyOrNotCollectionUrl()
    {
        var result = NexusUrlParser.Parse("https://www.nexusmods.com/games/readyornot/collections/abc123/revisions/7");

        var collection = Assert.IsType<NexusCollectionReference>(result);
        Assert.Equal("abc123", collection.Slug);
        Assert.Equal(7, collection.RevisionNumber);
        Assert.Equal("readyornot", collection.GameDomain);
    }

    [Fact]
    public void NexusUrlParser_ParsesReadyOrNotShorthandCollectionUrl()
    {
        var result = NexusUrlParser.Parse("https://www.nexusmods.com/readyornot/collections/abc123");

        var collection = Assert.IsType<NexusCollectionReference>(result);
        Assert.Equal("abc123", collection.Slug);
        Assert.Null(collection.RevisionNumber);
        Assert.Equal("readyornot", collection.GameDomain);
    }

    [Fact]
    public void NexusUrlParser_RejectsOtherGames()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            NexusUrlParser.Parse("https://www.nexusmods.com/games/skyrimspecialedition/mods/1"));

        Assert.Contains("Ready or Not", exception.Message);
    }

    [Fact]
    public void ArchiveScanner_FindsDeployableUnrealFilesInsideNestedZip()
    {
        var zipPath = CreateZip(("mods/loadout/MyMod.pak", "pak"), ("mods/loadout/MyMod.ucas", "ucas"), ("readme.txt", "ignore"));

        var files = ArchiveScanner.FindDeployableFiles(zipPath).Select(file => file.EntryPath).ToArray();

        Assert.Equal(["mods/loadout/MyMod.pak", "mods/loadout/MyMod.ucas"], files);
    }

    [Fact]
    public void ArchiveFormatDetector_DetectsRarHeaderEvenWhenFileHasZipExtension()
    {
        var path = Path.Combine(CreateTempDirectory(), "download.zip");
        File.WriteAllBytes(path, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00]);

        var format = ArchiveFormatDetector.Detect(path);

        Assert.Equal(ArchiveFormat.Rar, format);
        Assert.Equal(".rar", format.Extension);
    }

    [Fact]
    public void ArchiveFormatDetector_DetectsSevenZipHeaderWithSevenZipExtensionAlias()
    {
        var path = Path.Combine(CreateTempDirectory(), "download.7zip");
        File.WriteAllBytes(path, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04]);

        var format = ArchiveFormatDetector.Detect(path);

        Assert.Equal(ArchiveFormat.SevenZipLong, format);
        Assert.Equal(".7zip", format.Extension);
    }

    [Fact]
    public void ArchiveScanner_ExtractsDeployableFilesFromSevenZipArchive()
    {
        var archivePath = CreateSevenZip(("nested/SevenZipMod.pak", "pak"), ("nested/readme.txt", "ignore"));
        var destination = CreateTempDirectory();

        var deployed = ArchiveScanner.ExtractDeployableFiles(archivePath, destination);

        var deployedFile = Assert.Single(deployed);
        Assert.EndsWith("SevenZipMod.pak", deployedFile, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("pak", File.ReadAllText(deployedFile));
    }

    [Fact]
    public void ArchiveScanner_GroupsRelatedUnrealFilesByPakBaseName()
    {
        var archivePath = CreateZip(
            ("mods/Alpha.pak", "alpha pak"),
            ("mods/Alpha.ucas", "alpha ucas"),
            ("mods/Alpha.utoc", "alpha utoc"),
            ("mods/Beta.pak", "beta pak"),
            ("mods/Beta.sig", "beta sig"),
            ("readme.txt", "ignore"));

        var groups = ArchiveScanner.GetDeployableGroups(archivePath);

        Assert.Equal(["Alpha", "Beta"], groups.Select(group => group.DisplayName).ToArray());
        Assert.Equal(["mods/Alpha.pak", "mods/Alpha.ucas", "mods/Alpha.utoc"], groups[0].Files.Select(file => file.EntryPath).ToArray());
        Assert.Equal(["mods/Beta.pak", "mods/Beta.sig"], groups[1].Files.Select(file => file.EntryPath).ToArray());
    }

    [Fact]
    public void ArchiveScanner_ExtractsOnlySelectedDeployableEntries()
    {
        var archivePath = CreateZip(
            ("mods/Alpha.pak", "alpha pak"),
            ("mods/Alpha.ucas", "alpha ucas"),
            ("mods/Beta.pak", "beta pak"));
        var destination = CreateTempDirectory();

        var deployed = ArchiveScanner.ExtractDeployableFiles(archivePath, destination, ["mods/Alpha.pak", "mods/Alpha.ucas"]);

        Assert.Equal(2, deployed.Count);
        Assert.True(File.Exists(Path.Combine(destination, "Alpha.pak")));
        Assert.True(File.Exists(Path.Combine(destination, "Alpha.ucas")));
        Assert.False(File.Exists(Path.Combine(destination, "Beta.pak")));
    }

    [Fact]
    public void ArchiveScanner_ReportsIntermediateExtractionProgress()
    {
        var archivePath = CreateZip(
            ("mods/Alpha.pak", "alpha pak"),
            ("mods/Beta.pak", "beta pak"));
        var destination = CreateTempDirectory();
        var reports = new List<double>();

        ArchiveScanner.ExtractDeployableFiles(archivePath, destination, progress: new CaptureProgress(reports));

        Assert.Contains(reports, value => value > 0 && value < 1);
        Assert.Equal(1, reports.Last());
    }

    [Fact]
    public async Task DownloadManager_RenamesArchiveToDetectedFormat()
    {
        var directory = CreateTempDirectory();
        var payload = new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 };
        using var http = new HttpClient(new ByteStubHandler(payload));
        var downloader = new DownloadManager(http);

        var path = await downloader.DownloadAsync(new Uri("https://download.example/file"), directory, "mod.zip", null, CancellationToken.None);

        Assert.EndsWith(".rar", path);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(directory, "mod.zip")));
    }

    [Fact]
    public void InstallManifestStore_RoundTripsManifest()
    {
        var directory = CreateTempDirectory();
        var store = new InstallManifestStore(Path.Combine(directory, "manifest.json"));
        var manifest = new InstallManifest
        {
            Records =
            [
                new InstalledModRecord
                {
                    ModName = "Test Mod",
                    SourceUrl = "https://www.nexusmods.com/games/readyornot/mods/123",
                    ArchivePath = "C:\\Downloads\\test.zip",
                    InstalledAtUtc = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero),
                    DeployedFiles = ["C:\\Game\\ReadyOrNot\\Content\\Paks\\Test.pak"]
                }
            ]
        };

        store.Save(manifest);

        var loaded = store.Load();
        Assert.Single(loaded.Records);
        Assert.Equal("Test Mod", loaded.Records[0].ModName);
        Assert.Equal("C:\\Game\\ReadyOrNot\\Content\\Paks\\Test.pak", loaded.Records[0].DeployedFiles[0]);
    }

    [Fact]
    public void InstallManifestStore_LoadsOldManifestWithoutProfileFields()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, """
            {
              "Records": [
                {
                  "InstallId": "old",
                  "ModName": "Old Mod",
                  "SourceUrl": "https://example/mod",
                  "ArchivePath": "C:\\Mods\\old.zip",
                  "InstalledAtUtc": "2026-06-27T12:00:00+00:00",
                  "DeployedFiles": ["C:\\Game\\Old.pak"]
                }
              ]
            }
            """);

        var loaded = new InstallManifestStore(path).Load();

        var record = Assert.Single(loaded.Records);
        Assert.Equal("old", record.InstallId);
        Assert.Empty(record.ProfileId);
        Assert.Empty(record.SelectedArchiveEntries);
    }

    [Fact]
    public void LocalSettingsStore_ClearDeletesSavedSettings()
    {
        var directory = CreateTempDirectory();
        var store = new LocalSettingsStore(directory);
        store.Save(new LocalSettings
        {
            ApiKey = "secret-api-key",
            DownloadDirectory = "C:\\Downloads\\Mods",
            ReadyOrNotDirectory = "E:\\Steam\\Ready or Not",
            ImportDirectory = "C:\\Users\\Tester"
        });

        store.Clear();

        var loaded = store.Load();
        Assert.Empty(loaded.ApiKey);
        Assert.Empty(loaded.ReadyOrNotDirectory);
        Assert.DoesNotContain(Directory.GetFiles(directory), path => Path.GetFileName(path).Equals("settings.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalSettingsStore_LoadsOldSettingsWithSetupFieldsDefaulted()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "settings.json"), """
            {
              "DownloadDirectory": "C:\\Downloads\\Mods",
              "ReadyOrNotDirectory": "E:\\Steam\\Ready or Not",
              "ImportDirectory": "C:\\Users\\Tester"
            }
            """);

        var loaded = new LocalSettingsStore(directory).Load();

        Assert.False(loaded.SetupCompleted);
        Assert.False(loaded.ForceSetupWizard);
        Assert.False(loaded.AutoTestNexusOnLaunch);
        Assert.Equal(ThemeManager.DefaultThemeName, loaded.ThemeName);
    }

    [Fact]
    public void LocalSettingsStore_RoundTripsThemeName()
    {
        var directory = CreateTempDirectory();
        var store = new LocalSettingsStore(directory);

        store.Save(new LocalSettings { ThemeName = "hacker" });

        Assert.Equal("hacker", store.Load().ThemeName);
    }

    [Fact]
    public void LocalSettingsStore_RoundTripsAutoTestNexusOnLaunch()
    {
        var directory = CreateTempDirectory();
        var store = new LocalSettingsStore(directory);

        store.Save(new LocalSettings { AutoTestNexusOnLaunch = true });

        Assert.True(store.Load().AutoTestNexusOnLaunch);
    }

    [Fact]
    public void SetupGate_RequiresWizardWhenEssentialsAreMissingOrInvalid()
    {
        var root = CreateTempDirectory();
        var validGame = Path.Combine(root, "Ready or Not");
        Directory.CreateDirectory(Path.Combine(validGame, "ReadyOrNot", "Content", "Paks"));

        Assert.True(SetupGate.ShouldShowSetup(new LocalSettings { ApiKey = "", ReadyOrNotDirectory = validGame }));
        Assert.True(SetupGate.ShouldShowSetup(new LocalSettings { ApiKey = "abc", ReadyOrNotDirectory = Path.Combine(root, "Missing") }));
        Assert.True(SetupGate.ShouldShowSetup(new LocalSettings { ApiKey = "abc", ReadyOrNotDirectory = validGame, ForceSetupWizard = true }));
        Assert.False(SetupGate.ShouldShowSetup(new LocalSettings { ApiKey = "abc", ReadyOrNotDirectory = validGame, SetupCompleted = true }));
    }

    [Fact]
    public void DeploymentManager_ExtractsFilesToPaksFolderAndUninstallsManifestRecord()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var archive = CreateZip(("nested/Shield.pak", "pak"), ("nested/Shield.utoc", "utoc"));
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);

        var record = manager.Deploy(new DeploymentRequest(
            ModName: "Shield Pack",
            SourceUrl: "https://www.nexusmods.com/games/readyornot/mods/45",
            ArchivePath: archive,
            ReadyOrNotInstallDirectory: game));

        Assert.All(record.DeployedFiles, path => Assert.True(File.Exists(path)));
        Assert.Contains(record.DeployedFiles, path => path.EndsWith("Shield.pak", StringComparison.OrdinalIgnoreCase));
        Assert.Single(store.Load().Records);

        manager.Uninstall(record.InstallId);

        Assert.All(record.DeployedFiles, path => Assert.False(File.Exists(path)));
        Assert.Empty(store.Load().Records);
    }

    [Fact]
    public void DeploymentManager_UninstallsAllRecordsForProfile()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var archive = CreateZip(("nested/ProfileMod.pak", "pak"));
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);

        var first = manager.Deploy(new DeploymentRequest("Profile Mod", "https://example/1", archive, game, ProfileId: "profile-a"));
        var second = manager.Deploy(new DeploymentRequest("Other Mod", "https://example/2", archive, game, ProfileId: "profile-b"));

        manager.UninstallProfile("profile-a");

        Assert.All(first.DeployedFiles, path => Assert.False(File.Exists(path)));
        Assert.All(second.DeployedFiles, path => Assert.True(File.Exists(path)));
        Assert.Equal("profile-b", Assert.Single(store.Load().Records).ProfileId);
    }

    [Fact]
    public void DeploymentManager_RedeployDeletesPreviousFilesForSameProfileItem()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var archive = CreateZip(("nested/ProfileMod.pak", "pak"));
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);

        var first = manager.Deploy(new DeploymentRequest("Profile Mod", "https://example/1", archive, game, ProfileId: "profile-a"));
        var second = manager.Deploy(new DeploymentRequest("Profile Mod", "https://example/1", archive, game, ProfileId: "profile-a"));

        Assert.All(second.DeployedFiles, path => Assert.True(File.Exists(path)));
        Assert.DoesNotContain(second.DeployedFiles, path => Path.GetFileNameWithoutExtension(path).EndsWith("-1", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(first.DeployedFiles, second.DeployedFiles);
        Assert.Single(store.Load().Records);
    }

    [Fact]
    public void DeploymentManager_DeploysManyManualArchivesWithoutReplacingPreviousFiles()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);
        var archives = Enumerable
            .Range(1, 20)
            .Select(index => CreateZip(($"nested/Manual{index}.pak", $"pak {index}")))
            .ToArray();

        foreach (var archive in archives)
        {
            manager.Deploy(new DeploymentRequest(
                ModName: Path.GetFileNameWithoutExtension(archive),
                SourceUrl: string.Empty,
                ArchivePath: archive,
                ReadyOrNotInstallDirectory: game));
        }

        var manifest = store.Load();
        Assert.Equal(20, manifest.Records.Count);
        Assert.Equal(20, Directory.GetFiles(paks, "*.pak").Length);
    }

    [Fact]
    public void DeploymentManager_DeploysMultipleNexusFilesFromSameModPage()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var firstArchive = CreateZip(("nested/Main.pak", "pak"));
        var secondArchive = CreateZip(("nested/Optional.pak", "pak"));
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);
        const string sourceUrl = "https://www.nexusmods.com/readyornot/mods/7975";

        manager.Deploy(new DeploymentRequest("Main", sourceUrl, firstArchive, game, ModId: 7975, FileId: 28406));
        manager.Deploy(new DeploymentRequest("Optional", sourceUrl, secondArchive, game, ModId: 7975, FileId: 28407));

        var manifest = store.Load();
        Assert.Equal(2, manifest.Records.Count);
        Assert.Equal(2, Directory.GetFiles(paks, "*.pak").Length);
    }

    [Fact]
    public void DeploymentManager_RedeploysExplicitInstallRecordOnly()
    {
        var root = CreateTempDirectory();
        var game = Path.Combine(root, "ReadyOrNot");
        var paks = Path.Combine(game, "ReadyOrNot", "Content", "Paks");
        Directory.CreateDirectory(paks);
        var firstArchive = CreateZip(("nested/Alpha.pak", "old"));
        var secondArchive = CreateZip(("nested/Bravo.pak", "pak"));
        var replacementArchive = CreateZip(("nested/AlphaNew.pak", "new"));
        var store = new InstallManifestStore(Path.Combine(root, "manifest.json"));
        var manager = new DeploymentManager(store);

        var first = manager.Deploy(new DeploymentRequest("Alpha", string.Empty, firstArchive, game));
        var second = manager.Deploy(new DeploymentRequest("Bravo", string.Empty, secondArchive, game));
        var replacement = manager.Deploy(new DeploymentRequest(
            "Alpha",
            string.Empty,
            replacementArchive,
            game,
            ExistingInstallId: first.InstallId));

        Assert.All(first.DeployedFiles, path => Assert.False(File.Exists(path)));
        Assert.All(second.DeployedFiles, path => Assert.True(File.Exists(path)));
        Assert.All(replacement.DeployedFiles, path => Assert.True(File.Exists(path)));
        Assert.Equal(2, store.Load().Records.Count);
    }

    [Fact]
    public void ModProfileStore_SavesProfileAndCopiesArchivesIntoLibrary()
    {
        var root = CreateTempDirectory();
        var archive = CreateZip(("nested/ProfileMod.pak", "pak"));
        var store = new ModProfileStore(root);
        var profile = new ModProfile
        {
            Name = "SWAT Essentials",
            Items =
            [
                new ModProfileItem
                {
                    ModId = 10,
                    FileId = 20,
                    ModName = "Profile Mod",
                    Version = "1.0",
                    SourceUrl = "https://example/mod",
                    ArchivePath = archive,
                    SelectedArchiveEntries = ["nested/ProfileMod.pak"],
                    LastInstallId = "install-a"
                }
            ]
        };

        var saved = store.Save(profile, copyArchives: true);

        Assert.True(File.Exists(Path.Combine(root, saved.ProfileId, "profile.json")));
        Assert.Single(store.LoadAll());
        Assert.NotEqual(archive, saved.Items[0].ArchivePath);
        Assert.True(File.Exists(saved.Items[0].ArchivePath));
        Assert.Contains(Path.Combine(root, saved.ProfileId, "archives"), saved.Items[0].ArchivePath);
    }

    [Fact]
    public void ModProfilePlanner_UsesOnlyInstalledManifestRecords()
    {
        var installed = new InstalledModRecord
        {
            InstallId = "install-a",
            ModId = 10,
            FileId = 20,
            ModName = "Installed Mod",
            SourceUrl = "https://example/installed",
            ArchivePath = "installed.zip",
            SelectedArchiveEntries = ["Installed.pak"],
            DeployedFiles = ["Installed.pak"]
        };
        var uninstalledStaleRecord = new InstalledModRecord
        {
            InstallId = "install-b",
            ModId = 11,
            FileId = 21,
            ModName = "Removed Mod",
            SourceUrl = "https://example/removed",
            ArchivePath = "removed.zip"
        };

        var profile = ModProfilePlanner.FromInstalledRecords(
            new ModProfile { Name = "Current deployed set" },
            [installed, uninstalledStaleRecord]);

        var item = Assert.Single(profile.Items);
        Assert.Equal("Installed Mod", item.ModName);
        Assert.Equal("install-a", item.LastInstallId);
        Assert.Equal(["Installed.pak"], item.SelectedArchiveEntries);
    }

    [Fact]
    public void ErrorLogStore_AppendsAndClearsEntries()
    {
        var path = Path.Combine(CreateTempDirectory(), "error-log.json");
        var store = new ErrorLogStore(path);

        store.Append(new ErrorLogEntry
        {
            Operation = "Download",
            ModName = "Broken Mod",
            SourceUrl = "https://example/mod",
            Message = "Download failed",
            Detail = "stack"
        });

        var entry = Assert.Single(store.Load().Entries);
        Assert.Equal("Download", entry.Operation);
        Assert.Equal("Broken Mod", entry.ModName);

        store.Clear();

        Assert.Empty(store.Load().Entries);
    }

    [Fact]
    public async Task NexusClient_ReturnsDownloadUriFromOfficialApiShape()
    {
        using var http = new HttpClient(new StubHandler("""[{"URI":"https://download.example/mod.zip"}]"""));
        var client = new NexusClient(http, "abc123");

        var uri = await client.GetDownloadLinkAsync("readyornot", 12, 34, CancellationToken.None);

        Assert.Equal("https://download.example/mod.zip", uri.ToString());
    }

    [Fact]
    public async Task NexusClient_ValidatesApiKeyThroughOfficialValidateEndpoint()
    {
        using var handler = new StubHandler("""{"name":"Tester","is_premium":true}""");
        using var http = new HttpClient(handler);
        var client = new NexusClient(http, "abc123");

        var result = await client.ValidateApiKeyAsync(CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("Tester", result.UserName);
        Assert.Equal("https://api.nexusmods.com/v1/users/validate.json", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task NexusClient_ReturnsInvalidValidationResultForUnauthorizedKey()
    {
        using var http = new HttpClient(new StatusStubHandler(HttpStatusCode.Unauthorized, """{"message":"Invalid API key"}"""));
        var client = new NexusClient(http, "bad");

        var result = await client.ValidateApiKeyAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("401", result.Message);
    }

    [Fact]
    public void ReadyOrNotInstallDetector_FindsGameInSteamLibraryFolders()
    {
        var root = CreateTempDirectory();
        var steam = Path.Combine(root, "Steam");
        var library = Path.Combine(root, "Library");
        var game = Path.Combine(library, "steamapps", "common", "Ready Or Not");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateDirectory(Path.Combine(game, "ReadyOrNot", "Content", "Paks"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0" { "path" "{{steam.Replace("\\", "\\\\")}}" }
                "1" { "path" "{{library.Replace("\\", "\\\\")}}" }
            }
            """);

        var result = ReadyOrNotInstallDetector.FindInstallDirectory([steam]);

        Assert.Equal(game, result);
    }

    [Fact]
    public void DashboardSummaryFactory_CountsInstalledPendingAndRecentActivity()
    {
        var manifest = new InstallManifest
        {
            Records =
            [
                new InstalledModRecord { ModName = "Installed A", InstalledAtUtc = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero) },
                new InstalledModRecord { ModName = "Installed B", InstalledAtUtc = new DateTimeOffset(2026, 6, 27, 12, 10, 0, TimeSpan.Zero) }
            ]
        };
        var queue = new[]
        {
            new ModQueueItem { ModName = "Queued", Status = "Queued" },
            new ModQueueItem { ModName = "Done", Status = "Deployed" }
        };
        var log = new ErrorLog
        {
            Entries =
            [
                new ErrorLogEntry { Operation = "Download", ModName = "Broken", TimestampUtc = new DateTimeOffset(2026, 6, 27, 12, 20, 0, TimeSpan.Zero) }
            ]
        };

        var summary = DashboardSummaryFactory.Create(manifest, queue, log);

        Assert.Equal(2, summary.InstalledModCount);
        Assert.Equal(1, summary.PendingQueueCount);
        Assert.Equal("Download failed for Broken", summary.RecentActivity[0].Text);
        Assert.Equal("Installed B deployed", summary.RecentActivity[1].Text);
    }

    [Fact]
    public void ActivityLogStore_PersistsRecentMessagesNewestFirst()
    {
        var path = Path.Combine(CreateTempDirectory(), "activity-log.json");
        var store = new ActivityLogStore(path);

        store.Append("Imported 2 archive file(s).", new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero));
        store.Append("Added 36 collection item(s).", new DateTimeOffset(2026, 6, 27, 12, 5, 0, TimeSpan.Zero));

        var entries = new ActivityLogStore(path).Load().Entries;

        Assert.Equal(["Added 36 collection item(s).", "Imported 2 archive file(s)."], entries.Select(entry => entry.Message).ToArray());
    }

    [Fact]
    public void ActivityLogStore_ClearDeletesSavedMessages()
    {
        var path = Path.Combine(CreateTempDirectory(), "activity-log.json");
        var store = new ActivityLogStore(path);
        store.Append("Imported 2 archive file(s).");

        store.Clear();

        Assert.Empty(new ActivityLogStore(path).Load().Entries);
    }

    [Fact]
    public void DashboardSummaryFactory_IncludesActivityLogMessages()
    {
        var manifest = new InstallManifest
        {
            Records =
            [
                new InstalledModRecord { ModName = "Installed", InstalledAtUtc = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero) }
            ]
        };
        var queue = Array.Empty<ModQueueItem>();
        var errorLog = new ErrorLog
        {
            Entries =
            [
                new ErrorLogEntry { Operation = "Deploy", ModName = "Broken", TimestampUtc = new DateTimeOffset(2026, 6, 27, 12, 10, 0, TimeSpan.Zero) }
            ]
        };
        var activityLog = new ActivityLog
        {
            Entries =
            [
                new ActivityLogEntry { Message = "Imported 2 archive file(s).", TimestampUtc = new DateTimeOffset(2026, 6, 27, 12, 20, 0, TimeSpan.Zero) }
            ]
        };

        var summary = DashboardSummaryFactory.Create(manifest, queue, errorLog, activityLog);

        Assert.Equal("Imported 2 archive file(s).", summary.RecentActivity[0].Text);
        Assert.Equal("Deploy failed for Broken", summary.RecentActivity[1].Text);
        Assert.Equal("Installed deployed", summary.RecentActivity[2].Text);
    }

    [Fact]
    public void QueueDeploymentPlanner_SelectsOnlyDownloadedItems()
    {
        var selected = QueueDeploymentPlanner.GetDeployableDownloadedItems(
            [
                new ModQueueItem { ModName = "Alpha", ArchivePath = "C:\\Mods\\Alpha.zip", Status = "Downloaded" },
                new ModQueueItem { ModName = "Bravo", ArchivePath = "", Status = "Queued" },
                new ModQueueItem { ModName = "Charlie", ArchivePath = "C:\\Mods\\Charlie.7z", Status = "Imported archive" }
            ]);

        Assert.Equal(["Alpha", "Charlie"], selected.Select(item => item.ModName).ToArray());
    }

    [Fact]
    public void QueueDeploymentPlanner_RemovesSelectedItemsOnlyFromQueue()
    {
        var keep = new ModQueueItem { ModName = "Keep", ArchivePath = "C:\\Mods\\Keep.zip", InstallId = "install-1" };
        var remove = new ModQueueItem { ModName = "Remove", ArchivePath = "C:\\Mods\\Remove.zip", InstallId = "install-2" };
        var queue = new List<ModQueueItem> { keep, remove };

        var removed = QueueDeploymentPlanner.RemoveSelectedItems(queue, [remove]);

        Assert.Equal([keep], queue);
        Assert.Equal([remove], removed);
        Assert.Equal("C:\\Mods\\Remove.zip", remove.ArchivePath);
        Assert.Equal("install-2", remove.InstallId);
    }

    [Theory]
    [InlineData("Queued", VisualTone.Warning, "Queued")]
    [InlineData("Downloaded", VisualTone.Info, "Downloaded")]
    [InlineData("Missing archive", VisualTone.Warning, "Missing")]
    [InlineData("Missing zip", VisualTone.Warning, "Missing")]
    [InlineData("Deployed", VisualTone.Success, "Deployed")]
    [InlineData("Failed - see errors", VisualTone.Danger, "Error")]
    [InlineData("Something custom", VisualTone.Neutral, "Something custom")]
    public void QueueStatusVisual_MapsKnownStatuses(string status, VisualTone tone, string label)
    {
        var visual = QueueStatusVisual.FromStatus(status);

        Assert.Equal(tone, visual.Tone);
        Assert.Equal(label, visual.Label);
    }

    [Theory]
    [InlineData("Download failed for Broken", VisualTone.Danger)]
    [InlineData("Imported 2 archive file(s).", VisualTone.Info)]
    [InlineData("Selected installed mods uninstalled.", VisualTone.Warning)]
    [InlineData("Activated modpack: Tactical", VisualTone.Warning)]
    [InlineData("Story overhaul deployed", VisualTone.Success)]
    public void RecentActivityVisual_MapsActivitySeverity(string text, VisualTone expectedTone)
    {
        var item = new RecentActivityItem(new DateTimeOffset(2026, 6, 27, 14, 5, 0, TimeSpan.Zero), text);
        var expectedTime = item.TimestampUtc.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        var visual = RecentActivityVisual.FromActivity(item);

        Assert.Equal(expectedTone, visual.Tone);
        Assert.Equal(text, visual.Text);
        Assert.Equal(expectedTime, visual.TimeText);
    }

    [Theory]
    [InlineData("Detected", DashboardStatusKind.Game, VisualTone.Success)]
    [InlineData("Not detected", DashboardStatusKind.Game, VisualTone.Danger)]
    [InlineData("Connected: Tester", DashboardStatusKind.Nexus, VisualTone.Success)]
    [InlineData("Not tested", DashboardStatusKind.Nexus, VisualTone.Warning)]
    [InlineData("Missing key", DashboardStatusKind.Nexus, VisualTone.Danger)]
    [InlineData("Current: v1.3.3 | Unable to check", DashboardStatusKind.Update, VisualTone.Warning)]
    public void DashboardStatusVisual_MapsConnectionStates(string status, DashboardStatusKind kind, VisualTone expectedTone)
    {
        var visual = DashboardStatusVisual.FromStatus(kind, status);

        Assert.Equal(expectedTone, visual.Tone);
        Assert.False(string.IsNullOrWhiteSpace(visual.HelperText));
    }

    [Fact]
    public void ThemeManager_ProvidesNamedThemesAndFallsBackToDefault()
    {
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "claude");
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "codex");
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "dark");
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "light");
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "purple");
        Assert.Contains(ThemeManager.Themes, theme => theme.Name == "hacker");
        Assert.DoesNotContain(ThemeManager.Themes, theme => theme.Name == "chatgpt");

        Assert.Equal(ThemeManager.DefaultThemeName, ThemeManager.ResolveThemeName("missing"));
    }

    [Fact]
    public void ThemeManager_ReplacesFrozenBrushResources()
    {
        var resources = new ResourceDictionary();
        var frozenBrush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
        frozenBrush.Freeze();
        resources["ShellBrush"] = frozenBrush;

        ThemeManager.ApplyTheme(resources, "claude");

        var updatedBrush = Assert.IsType<LinearGradientBrush>(resources["ShellBrush"]);
        Assert.False(updatedBrush.IsFrozen);
        Assert.NotSame(frozenBrush, updatedBrush);
    }

    [Fact]
    public void ThemeManager_AppliesVisibleGradientSurfaceBrushes()
    {
        var resources = new ResourceDictionary();

        ThemeManager.ApplyTheme(resources, "codex");

        Assert.IsType<LinearGradientBrush>(resources["ShellBrush"]);
        Assert.IsType<LinearGradientBrush>(resources["RailBrush"]);
        Assert.IsType<LinearGradientBrush>(resources["PanelBrush"]);
        Assert.IsType<LinearGradientBrush>(resources["PanelAltBrush"]);
        Assert.IsType<SolidColorBrush>(resources["TextBrush"]);
    }

    [Fact]
    public async Task AppUpdateChecker_ReportsUpToDateAndAvailableAndUnavailable()
    {
        var current = new Version(1, 2, 4, 0);
        using var upToDateHttp = new HttpClient(new StatusStubHandler(HttpStatusCode.OK, """{"tag_name":"v1.2.4"}"""));
        using var updateHttp = new HttpClient(new StatusStubHandler(HttpStatusCode.OK, """{"tag_name":"v1.2.5"}"""));
        using var failedHttp = new HttpClient(new StatusStubHandler(HttpStatusCode.InternalServerError, "{}"));

        var upToDate = await new AppUpdateChecker(upToDateHttp).CheckLatestAsync(current, CancellationToken.None);
        var update = await new AppUpdateChecker(updateHttp).CheckLatestAsync(current, CancellationToken.None);
        var failed = await new AppUpdateChecker(failedHttp).CheckLatestAsync(current, CancellationToken.None);

        Assert.Equal(AppUpdateStatus.UpToDate, upToDate.Status);
        Assert.Equal(AppUpdateStatus.UpdateAvailable, update.Status);
        Assert.Equal("v1.2.5", update.LatestTag);
        Assert.Equal(AppUpdateStatus.UnableToCheck, failed.Status);
    }

    [Fact]
    public void AppUpdateRefreshGate_AllowsForcedManualRetryAfterInitialCheck()
    {
        var gate = new AppUpdateRefreshGate();

        Assert.True(gate.ShouldCheck(force: false));
        Assert.False(gate.ShouldCheck(force: false));
        Assert.True(gate.ShouldCheck(force: true));
    }

    [Fact]
    public void ModpackShareStore_ExportsOnlySafeNexusMetadata()
    {
        var path = Path.Combine(CreateTempDirectory(), "shared.ronmodpack.json");
        var profile = new ModProfile
        {
            Name = "Entry Team",
            Items =
            [
                new ModProfileItem
                {
                    ModId = 10,
                    FileId = 20,
                    ModName = "Uniform",
                    Version = "1.0",
                    SourceUrl = "https://www.nexusmods.com/readyornot/mods/10",
                    ArchivePath = "C:\\Private\\Uniform.zip",
                    SelectedArchiveEntries = ["Uniform.pak"],
                    LastInstallId = "install-secret"
                },
                new ModProfileItem
                {
                    ModName = "Manual Local Only",
                    ArchivePath = "C:\\Private\\Manual.zip"
                }
            ]
        };

        var result = ModpackShareStore.Export(profile, path, new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero));
        var imported = ModpackShareStore.Import(path);
        var item = Assert.Single(imported.Items);
        var json = File.ReadAllText(path);

        Assert.Equal(1, result.ExportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("ReadyOrNotModManager.ModpackLinks", imported.Format);
        Assert.Equal("Entry Team", imported.ModpackName);
        Assert.Equal(10, item.ModId);
        Assert.Equal(20, item.FileId);
        Assert.Equal("https://www.nexusmods.com/readyornot/mods/10", item.SourceUrl);
        Assert.DoesNotContain("Private", json);
        Assert.DoesNotContain("install-secret", json);
        Assert.DoesNotContain("Uniform.pak", json);
    }

    [Fact]
    public void ModpackShareStore_ImportsAsUniqueProfileWithEmptyArchives()
    {
        var library = CreateTempDirectory();
        var store = new ModProfileStore(library);
        store.Save(new ModProfile { Name = "Entry Team" }, copyArchives: false);
        var share = new ModpackShareFile
        {
            ModpackName = "Entry Team",
            Items =
            [
                new ModpackShareItem
                {
                    ModId = 10,
                    FileId = 20,
                    ModName = "Uniform",
                    Version = "1.0",
                    SourceUrl = "https://www.nexusmods.com/readyornot/mods/10"
                }
            ]
        };

        var profile = ModpackShareStore.ToProfile(share, store, new DateTimeOffset(2026, 6, 28, 12, 10, 0, TimeSpan.Zero));
        var item = Assert.Single(profile.Items);

        Assert.Equal("Entry Team imported 2026-06-28 12-10", profile.Name);
        Assert.Equal(string.Empty, item.ArchivePath);
        Assert.Equal(string.Empty, item.LastInstallId);
        Assert.Empty(item.SelectedArchiveEntries);
        Assert.Equal("https://www.nexusmods.com/readyornot/mods/10", item.SourceUrl);
    }

    [Fact]
    public void ProfileExportState_DisablesExportWhenNoProfilesExist()
    {
        var empty = ProfileExportState.FromProfiles([]);
        var populated = ProfileExportState.FromProfiles([new ModProfile { Name = "Entry Team" }]);

        Assert.False(empty.CanExport);
        Assert.Equal("No saved modpacks available to export.", empty.Message);
        Assert.True(populated.CanExport);
        Assert.Equal("Choose a saved local modpack to export.", populated.Message);
    }

    [Fact]
    public void ModProfileStore_RenamesProfileAndRejectsInvalidNames()
    {
        var root = CreateTempDirectory();
        var store = new ModProfileStore(root);
        var profile = store.Save(new ModProfile { Name = "Old name" }, copyArchives: false);

        var renamed = store.Rename(profile.ProfileId, "New name");

        Assert.True(renamed.Success);
        Assert.Equal("New name", store.Load(profile.ProfileId)?.Name);
        Assert.False(store.Rename(profile.ProfileId, " ").Success);
    }

    [Fact]
    public void ReadyOrNotLauncher_PrefersSteamLaunch()
    {
        var root = CreateTempDirectory();

        var result = ReadyOrNotLauncher.Resolve(root, preferSteam: true);

        Assert.True(result.CanLaunch);
        Assert.Equal("steam://rungameid/1144200", result.Target);
        Assert.True(result.UseShellExecute);
    }

    [Fact]
    public void ReadyOrNotLauncher_FallsBackToDirectExecutable()
    {
        var root = CreateTempDirectory();
        var executable = Path.Combine(root, "ReadyOrNot", "Binaries", "Win64", "ReadyOrNot-Win64-Shipping.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, string.Empty);

        var result = ReadyOrNotLauncher.Resolve(root, preferSteam: false);

        Assert.True(result.CanLaunch);
        Assert.Equal(executable, result.Target);
        Assert.Equal(Path.GetDirectoryName(executable), result.WorkingDirectory);
        Assert.True(result.UseShellExecute);
    }

    [Fact]
    public void ReadyOrNotLauncher_ReturnsFailureWhenNoLaunchTargetExists()
    {
        var result = ReadyOrNotLauncher.Resolve(CreateTempDirectory(), preferSteam: false);

        Assert.False(result.CanLaunch);
        Assert.Contains("Ready or Not executable", result.Message);
    }

    [Fact]
    public void ArchiveImportPlanner_AttachesMultipleArchivesToQueue()
    {
        var first = Path.Combine(CreateTempDirectory(), "Alpha.zip");
        var second = Path.Combine(CreateTempDirectory(), "Bravo.7z");
        var queue = new List<ModQueueItem>
        {
            new()
            {
                ModName = "Existing",
                Status = "Queued"
            }
        };

        ArchiveImportPlanner.ImportArchives(queue, queue[0], [first, second], "profile-a");

        Assert.Equal(2, queue.Count);
        Assert.Equal(first, queue[0].ArchivePath);
        Assert.Equal("Imported archive", queue[0].Status);
        Assert.Equal("Bravo", queue[1].ModName);
        Assert.Equal(second, queue[1].ArchivePath);
        Assert.Equal("Manual", queue[1].Version);
        Assert.Equal("profile-a", queue[1].ProfileId);
    }

    [Fact]
    public async Task NexusClient_UsesCanonicalReadyOrNotModPageForFiles()
    {
        using var http = new HttpClient(new StubHandler("""
            {
              "files": [
                { "file_id": 28406, "name": "Main", "version": "1.0", "category_id": 1, "is_primary": true }
              ]
            }
            """));
        var client = new NexusClient(http, "abc123");

        var file = Assert.Single(await client.GetModFilesAsync("readyornot", 7975, CancellationToken.None));

        Assert.Equal("https://www.nexusmods.com/readyornot/mods/7975", file.SourceUrl);
    }

    [Fact]
    public async Task CollectionResolver_ReturnsModFilesFromLatestPublishedRevisionWhenUrlHasNoRevision()
    {
        var json = """
            {
              "data": {
                "collection": {
                  "latestPublishedRevision": {
                    "modFiles": [
                      {
                        "fileId": 200,
                        "version": "1.2.0",
                        "file": { "fileId": 200, "modId": 100, "name": "Main file", "version": "1.2.0", "mod": { "name": "AI Overhaul" } }
                      }
                    ]
                  }
                }
              }
            }
            """;
        using var handler = new StubHandler(json);
        using var http = new HttpClient(handler);
        var resolver = new CollectionResolver(http, "abc123");

        var files = await resolver.ResolveLatestPublishedRevisionAsync(
            new NexusCollectionReference("readyornot", "abc123", null, "https://www.nexusmods.com/games/readyornot/collections/abc123"),
            CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal(100, file.ModId);
        Assert.Equal(200, file.FileId);
        Assert.Equal("AI Overhaul - Main file", file.Name);
        Assert.Equal("1.2.0", file.Version);
        Assert.Equal("https://api.nexusmods.com/v2/graphql", handler.LastRequestUri?.ToString());
        Assert.Contains("latestPublishedRevision", handler.LastRequestBody);
    }

    [Fact]
    public async Task CollectionResolver_ReturnsModFilesFromExplicitRevisionWhenUrlHasRevision()
    {
        var json = """
            {
              "data": {
                "collectionRevision": {
                  "modFiles": [
                    {
                      "fileId": 201,
                      "version": "2.0.0",
                      "file": { "fileId": 201, "modId": 101, "name": "Optional file", "version": "2.0.0", "mod": { "name": "AI Overhaul" } }
                    }
                  ]
                }
              }
            }
            """;
        using var handler = new StubHandler(json);
        using var http = new HttpClient(handler);
        var resolver = new CollectionResolver(http, "abc123");

        var files = await resolver.ResolveLatestPublishedRevisionAsync(
            new NexusCollectionReference("readyornot", "abc123", 7, "https://www.nexusmods.com/games/readyornot/collections/abc123/revisions/7"),
            CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal(101, file.ModId);
        Assert.Equal(201, file.FileId);
        Assert.Equal("AI Overhaul - Optional file", file.Name);
        Assert.Equal("2.0.0", file.Version);
        Assert.Contains("collectionRevision", handler.LastRequestBody);
        Assert.Contains("revisionNumber", handler.LastRequestBody);
    }

    private static string CreateZip(params (string Entry, string Content)[] entries)
    {
        var zipPath = Path.Combine(CreateTempDirectory(), "mod.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (entry, content) in entries)
        {
            var zipEntry = archive.CreateEntry(entry);
            using var writer = new StreamWriter(zipEntry.Open());
            writer.Write(content);
        }

        return zipPath;
    }

    private static string CreateSevenZip(params (string Entry, string Content)[] entries)
    {
        var archivePath = Path.Combine(CreateTempDirectory(), "mod.7z");
        using var writer = WriterFactory.OpenWriter(archivePath, ArchiveType.SevenZip, new WriterOptions(CompressionType.LZMA));
        foreach (var (entry, content) in entries)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            writer.Write(entry, stream, DateTime.UtcNow);
        }

        return archivePath;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ron-mod-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Assert.Equal("abc123", request.Headers.GetValues("apikey").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        }
    }

    private sealed class ByteStubHandler(byte[] responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBody)
            });
        }
    }

    private sealed class StatusStubHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });
        }
    }

    private sealed class CaptureProgress(List<double> reports) : IProgress<double>
    {
        public void Report(double value)
        {
            reports.Add(value);
        }
    }
}
