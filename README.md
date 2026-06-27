# Ready or Not Nexus Mod Manager

Version: `1.0.0`

A Windows desktop utility for queueing Ready or Not mods from Nexus Mods, downloading archives through Nexus-supported API flows, expanding collections into individual mod files, and deploying Unreal mod files into the Ready or Not `Content\Paks` directory.

## Requirements

- Windows 11 is the primary target.
- A Nexus Mods API key from the end user's own Nexus account.
- Ready or Not installed locally.

The app can be published as a self-contained Windows executable, so users do not need to install the .NET SDK.

## Run from source

```powershell
dotnet run --project .\ReadyOrNotModManager.App\ReadyOrNotModManager.App.csproj
```

## Publish a portable build

```powershell
dotnet publish .\ReadyOrNotModManager.App\ReadyOrNotModManager.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\win-x64
```

The executable will be written to `publish\win-x64`.

## Basic use

1. Paste and save your Nexus Mods API key.
   - Use **Get API key** in the app to open the Nexus account API access page.
2. Choose a download folder for retained zip archives.
3. Choose the Ready or Not install folder. The app expects `ReadyOrNot\Content\Paks` beneath that folder.
4. Paste a Ready or Not Nexus mod URL or collection URL.
5. Download missing files. If Nexus requires browser-based download, the app opens the Nexus page and you can import the completed archive.
6. Deploy selected queue items.

The app stores settings under `%LOCALAPPDATA%\ReadyOrNotModManager`; the API key is protected with Windows user-scope DPAPI. Deployed files are tracked in a local install manifest so selected mods can be uninstalled later.

The downloader supports ZIP, RAR, and 7z archives. If Nexus serves a RAR or 7z file when the queue expected a zip, the app detects the real archive type and corrects the saved extension.

Use **Import archive** to attach a manually downloaded ZIP/RAR/7z file to the selected queue row. If nothing is selected, the app creates a standalone imported-archive row. Use **Delete download** to remove selected archive files from disk without uninstalling deployed mod files.

Use **Clear user data** to remove the locally saved API key, selected folders, local install manifest, and current queue. It does not delete downloaded archives or deployed game files.

## Notes

This app does not scrape Nexus pages, bypass Nexus download restrictions, redistribute mods, or bundle mod archives. It uses Nexus API calls where available and falls back to the user's browser for downloads that require normal Nexus website handling.
