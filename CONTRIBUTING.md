# Developer Guide

This document covers the project layout, architecture, key subsystems, data storage, build instructions, and testing approach for contributors.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 (WPF target; the `Core` library builds cross-platform but the app does not)

## Project layout

```
ReadyOrNotModManager.sln
├── ReadyOrNotModManager.Core/        # Business logic — no WPF dependency
│   ├── Archives/                     # Format detection, scanning, extraction
│   ├── Deployment/                   # Deploy mod files into the game Paks folder
│   ├── Diagnostics/                  # Error log store
│   ├── Downloads/                    # HTTP download with progress
│   ├── Manifest/                     # Install manifest (deployed file tracking)
│   ├── Nexus/                        # Nexus REST + GraphQL API client, URL parser
│   └── Profiles/                     # Modpack profiles and library store
├── ReadyOrNotModManager.App/         # WPF desktop UI (net10.0-windows)
│   ├── Services/                     # UI-facing services, planners, visual helpers
│   ├── MainWindow.xaml / .cs         # Single-window shell with page navigation
│   ├── ProfilesWindow.xaml / .cs     # Modpack management dialog
│   ├── ArchiveSelectionWindow.xaml   # Advanced archive entry picker
│   ├── ErrorsWindow.xaml / .cs       # Error log viewer
│   └── LocalSettings.cs             # Settings model + store
└── ReadyOrNotModManager.Tests/       # xUnit tests for Core and App.Services
```

`Core` has no WPF reference and no platform-specific dependency. All business logic lives there so it can be tested without a UI host. `App` holds presentation code only, plus the thin `Services/` layer that bridges Core with WPF concerns (activity logging, dashboard data assembly, UI visual mapping).

## Architecture overview

```
┌─────────────────────────────────────────┐
│            WPF MainWindow               │
│  (queue, installed mods, profiles, UI)  │
└────────────┬────────────────────────────┘
             │ calls
┌────────────▼────────────────────────────┐
│         App/Services/                   │
│  ActivityLog, DashboardSummary,         │
│  ArchiveImportPlanner, ThemeManager,    │
│  QueueDeploymentPlanner, …              │
└────────────┬────────────────────────────┘
             │ calls
┌────────────▼────────────────────────────┐
│         Core/                           │
│  NexusClient, CollectionResolver,       │
│  DownloadManager, ArchiveScanner,       │
│  DeploymentManager, ModProfileStore, …  │
└─────────────────────────────────────────┘
```

Data flows are synchronous where possible. Long-running operations (download, deploy) are dispatched with `Task.Run` from `MainWindow` handlers and report progress via `IProgress<double>`.

## Core subsystems

### Nexus API (`Core/Nexus/`)

| Class | Responsibility |
|---|---|
| `NexusUrlParser` | Parses a Nexus URL string into a `NexusModReference` or `NexusCollectionReference`. Accepts both `/games/<domain>/mods/<id>` and shorthand `/<domain>/mods/<id>` forms. Rejects non-Ready-or-Not domains with a descriptive `ArgumentException`. |
| `NexusClient` | REST API wrapper. Calls `/users/validate.json` to verify an API key, `/games/{domain}/mods/{id}/files.json` to list mod files, and `/games/{domain}/mods/{id}/files/{fileId}/download_link.json` to get a CDN URI. Sends `apikey` header on every request. |
| `CollectionResolver` | GraphQL v2 client. Resolves a `NexusCollectionReference` into a flat `IReadOnlyList<NexusModFile>` by querying `latestPublishedRevision` (no revision number) or `collectionRevision` (explicit revision). |
| `NexusReferences` | Discriminated union root `NexusReference` with subtypes `NexusModReference` and `NexusCollectionReference`. Both carry `GameDomain`, `SourceUrl`, and type-specific fields. |

### Archive handling (`Core/Archives/`)

| Class | Responsibility |
|---|---|
| `ArchiveFormatDetector` | Reads the first 8 bytes of a file and matches against ZIP (`PK\x03\x04`), RAR (`Rar!`), and 7z (`7z\xBC\xAF`) magic bytes. Returns an `ArchiveFormat` record with the canonical extension. |
| `ArchiveScanner` | `FindDeployableFiles` — traverses a ZIP, RAR, or 7z archive and returns entries with extensions `.pak`, `.ucas`, `.utoc`, or `.sig`. `GetDeployableGroups` — groups those entries by base name (e.g. `MyMod.pak` + `MyMod.ucas` → one group named `MyMod`). `ExtractDeployableFiles` — extracts either all deployable entries or a caller-specified subset to a destination directory, reporting `IProgress<double>`. |

### Downloads (`Core/Downloads/`)

`DownloadManager.DownloadAsync` streams a URI to a file, reports byte-level progress, then calls `ArchiveFormatDetector` and renames the file to the detected extension if it does not match the download's stated extension.

### Deployment (`Core/Deployment/`)

`DeploymentManager.Deploy` receives a `DeploymentRequest` (mod name, source URL, archive path, install directory, optional profile ID, mod/file IDs, optional selected entries). It:

1. Resolves existing manifest records that match the request (by `ExistingInstallId`, by `ModId+FileId`, by archive path, or by source URL) and deletes their deployed files.
2. Calls `ArchiveScanner.ExtractDeployableFiles` to copy entries into `Content/Paks`.
3. Appends a new `InstalledModRecord` to the manifest and saves.

`Uninstall` and `UninstallProfile` delete deployed files and remove matching manifest records. `ReadyOrNotPaths.GetPaksDirectory` and `LooksLikeInstallDirectory` centralise the game path shape.

### Manifest (`Core/Manifest/`)

`InstallManifestStore` serialises `InstallManifest` (a list of `InstalledModRecord`) to a JSON file. Each record carries:

- `InstallId` — stable GUID assigned at deploy time
- `ModId`, `FileId` — Nexus identifiers (0 for manual imports)
- `ProfileId` — empty string for the default profile
- `DeployedFiles` — absolute paths of files written to `Paks`
- `SelectedArchiveEntries` — which archive paths were chosen in the advanced picker
- `InstalledAtUtc` — UTC timestamp

Old manifests missing newer fields round-trip safely; missing fields default to their zero/empty values.

### Profiles (`Core/Profiles/`)

`ModProfileStore` stores each modpack as a subdirectory under the configured library folder:

```
{libraryDirectory}/{profileId}/profile.json
{libraryDirectory}/{profileId}/archives/{archive files}
```

`Save(profile, copyArchives: true)` copies referenced archive files into the profile's `archives/` subdirectory (skipping files already inside it). `LoadAll` reads every `profile.json` found in the directory tree and returns them sorted by name. `Rename` validates uniqueness before patching the name field.

## App services (`App/Services/`)

| Service | Responsibility |
|---|---|
| `ActivityLogStore` | Appends timestamped text messages to a JSON file, newest-first, capped at a fixed maximum. |
| `AppUpdateChecker` | GETs the GitHub releases API, compares the latest tag's version with the running assembly version, returns `AppUpdateStatus` (UpToDate / UpdateAvailable / UnableToCheck). |
| `AppUpdateRefreshGate` | Ensures the update check runs at most once per session automatically; `ShouldCheck(force: true)` always passes. |
| `ArchiveImportPlanner` | Distributes a list of selected archive paths across the queue: the first goes to the selected row, additional paths become new standalone rows. |
| `DashboardSummaryFactory` | Merges manifest records, queue items, error log entries, and activity log entries into a `DashboardSummary` with counts and a sorted `RecentActivityItem` list. |
| `ModpackShareStore` | `Export` writes a `.ronmodpack.json` file containing only Nexus mod IDs, file IDs, and source URLs — no local paths, no API keys, no install identifiers. `Import` reads the file back. `ToProfile` converts an imported share into a `ModProfile` with empty `ArchivePath` and `LastInstallId` fields so the user downloads fresh. |
| `ModProfilePlanner` | Builds a `ModProfile` from the currently installed manifest records (only records with `DeployedFiles` count as truly installed). |
| `ProfileExportState` | Computes whether the export controls should be enabled and what helper text to show. |
| `QueueDeploymentPlanner` | `GetDeployableDownloadedItems` filters to rows with a non-empty archive path and a status of `"Downloaded"` or `"Imported archive"`. `RemoveSelectedItems` removes items from the queue list and returns them. |
| `ReadyOrNotInstallDetector` | Walks Steam's `libraryfolders.vdf` to locate the Ready or Not install directory. |
| `ReadyOrNotLauncher` | `Resolve` returns a `LaunchTarget` with the Steam URI (`steam://rungameid/1144200`) or the direct executable path, depending on `preferSteam`. `FindDirectExecutable` prefers `ReadyOrNotSteam-Win64-Shipping.exe` over the legacy executable name. |
| `SetupGate` | Returns `true` when the app should show the setup wizard: missing API key, non-existent game directory, or `ForceSetupWizard` flag set. |
| `ThemeManager` | Defines the theme catalogue as an `IReadOnlyList<AppTheme>` of named records. `ApplyTheme` writes `LinearGradientBrush` and `SolidColorBrush` values into a `ResourceDictionary`, replacing frozen brushes rather than mutating them. |
| `UiVisuals` | Pure mapping functions: `QueueStatusVisual.FromStatus`, `RecentActivityVisual.FromActivity`, `DashboardStatusVisual.FromStatus` — each returns a `VisualTone` enum value and display text/label. |
| `WindowIconProvider` | Extracts the icon embedded in the game executable using `System.Drawing.Icon.ExtractAssociatedIcon` and converts it to a WPF `BitmapSource`. Falls back to the bundled app icon. |

## Data storage

All user data is written to `%LOCALAPPDATA%\ReadyOrNotModManager`. None of these files are included in the repository or portable release ZIP.

| File / path | Content |
|---|---|
| `settings.json` | Download directory, game folder, theme name, setup flags. The API key is encrypted inline with Windows DPAPI (user scope). |
| `manifest.json` | Array of `InstalledModRecord` objects describing every deployed mod. |
| `error-log.json` | Append-only array of `ErrorLogEntry` objects. |
| `activity-log.json` | Bounded array of `ActivityLogEntry` messages, newest first. |
| `{modpackLibrary}/{profileId}/profile.json` | Modpack metadata and item list. |
| `{modpackLibrary}/{profileId}/archives/` | Archive files copied when saving a modpack. |

The modpack library folder is user-configured in Settings and can live anywhere.

## Build and run

```powershell
# Run from source
dotnet run --project .\ReadyOrNotModManager.App\ReadyOrNotModManager.App.csproj

# Run tests
dotnet test

# Publish a self-contained single-file executable
dotnet publish .\ReadyOrNotModManager.App\ReadyOrNotModManager.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o .\publish\win-x64
```

## Testing

Tests live in `ReadyOrNotModManager.Tests` and target `net10.0-windows` (WPF types are referenced from App.Services). The test file imports both `Core` and `App` projects.

Conventions:
- Tests use real file I/O against temp directories created with `Path.GetTempPath()`. No in-memory mocks for the file system.
- HTTP calls use sealed `HttpMessageHandler` stubs (`StubHandler`, `StatusStubHandler`, `ByteStubHandler`) defined at the bottom of the test file.
- Each logical behaviour gets its own `[Fact]`. Parameterised status/visual mapping tables use `[Theory]` + `[InlineData]`.
- Test method names follow `SubjectClass_DescribesBehaviourUnderTest` or `SubjectClass_ActionOnState` patterns.

When adding a new Core service, add at least one fact that exercises the happy path and one that verifies a failure or boundary condition using real file system state.

## Contributing

1. Keep `Core` free of WPF dependencies. Anything that touches `System.Windows.*` or `MahApps.*` belongs in `App` or `App/Services/`.
2. Prefer immutable `record` types for data transfer objects. Use mutable classes only where the object has a managed identity (e.g. `ModQueueItem` bound to a WPF `ObservableCollection`).
3. Match existing patterns for new Nexus API calls: typed DTO classes with `[JsonPropertyName]`, result types as records, exceptions as named subclasses of `Exception`.
4. All archive extraction goes through `ArchiveScanner` so format detection and progress reporting stay centralised.
5. Modpack share files must never include local file paths, API keys, or install identifiers — enforce this at the `ModpackShareStore.Export` boundary as the existing tests demonstrate.
6. Run `dotnet test` before submitting a pull request. CI is not yet configured, so the reviewer will run tests locally.
