using System.IO.Compression;
using System.Net;
using ReadyOrNotModManager.Core.Archives;
using ReadyOrNotModManager.Core.Deployment;
using ReadyOrNotModManager.Core.Manifest;
using ReadyOrNotModManager.Core.Nexus;
using ReadyOrNotModManager.Core.Downloads;
using ReadyOrNotModManager.Core.Diagnostics;
using ReadyOrNotModManager.Core.Profiles;
using ReadyOrNotModManager.App;
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
}
