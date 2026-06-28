# Ready or Not Nexus Mod Manager

Version: `1.3.9`

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

1. Complete the setup wizard on first launch.
   - Use **Get API key** to open the Nexus account API access page.
   - Use **Test Connection** to validate the key.
   - Use **Auto-detect Game Folder** or choose the Ready or Not install folder manually.
2. Use the dashboard quick actions to add Nexus URLs, import archives, deploy all downloaded queue items, open mod folders, show modpacks, or run the game.
3. Use the navigation sidebar for Dashboard, Installed, Mods, Modpacks, Utilities, Settings, and Logs/Errors.
4. Edit API/folder settings later from **Settings**. Use **Reset setup wizard** there to rerun the guided setup flow.

The app stores settings, logs, manifests, and queue data under `%LOCALAPPDATA%\ReadyOrNotModManager`; the API key is protected with Windows user-scope DPAPI. These files are local to each Windows user and are not included in the GitHub repository or portable release zip. Deployed files are tracked in a local install manifest so selected mods can be uninstalled later.

The main dashboard shows game detection, Nexus connection, installed mod count, pending queue count, recent activity, and common quick actions. Recent activity is shown as structured event rows with timestamps, icons, and status colour.

The Mods page has a Nexus URL toolbar, search, status filter, queue summary, status badges, and grouped action buttons. Button colours follow the action type: blue for normal/info, green for success/deploy/run game, amber for warning/modpacks, red for destructive/error, and grey for inactive.

Use **Remove selected** on the Mods page to remove accidental or duplicate queue entries without deleting downloaded archives or uninstalling deployed files.

The downloader supports ZIP, RAR, 7z, and `.7zip` archive aliases. If Nexus serves a RAR or 7z file when the queue expected a zip, the app detects the real archive type and corrects the saved extension.

Use **Import archive** to attach a manually downloaded ZIP/RAR/7z file to the selected queue row. If nothing is selected, the app creates a standalone imported-archive row. Use **Delete download** to remove selected archive files from disk without uninstalling deployed mod files.

Use **Advanced options** when you want to choose which `.pak` groups deploy from an archive that contains multiple mod variants. The app groups matching `.pak`, `.ucas`, `.utoc`, and `.sig` files together so the selected variant deploys with its required companion files.

Use **Modpacks** to save the currently installed/deployed mods as a local profile, load a profile back into the queue, rename a modpack from the right-click menu, or activate a profile. Activating a profile uninstalls files tracked for the previously active profile, then deploys the chosen profile's available archives. Profile data and copied archives are stored in the configured modpack library folder.

Use **Utilities** to export a saved modpack as a `.ronmodpack.json` link file, import one from another user, manage local archives, and see planned repair tools. These share files contain Nexus mod links and IDs only, not downloaded archives, API keys, deployed files, or local folders. Imported modpacks are saved locally and loaded into the Mods queue so the receiving user can download the same mods with their own Nexus account.

Long download and deploy operations report overall progress in the bottom status bar. Failed items are skipped so the rest of the queue can continue.

Version 1.3.9 restores the runtime Ready or Not executable icon for the taskbar/header when the game folder is configured, while keeping the new bundled tactical headset icon as the default fallback. Version 1.3.8 added the cleaner ImageMagick-generated custom app icon, support/social links, and recent sidebar/titlebar/dashboard polish.

Use the **Colour scheme** selector in Settings to switch between Tactical default, Dark mode, Light mode, Claude palette, Codex palette, Purple gradient, and Hacker themes. The selected theme is saved locally and applied on future launches.

Use **Errors** to review failed download/deploy items. The error page can open the Nexus page, game folder, archive folder, or copy a manual-fix note for the selected failure.

Use **Clear user data** to remove the locally saved API key, selected folders, local install manifest, error log, and current queue. It does not delete downloaded archives, profile library files, or deployed game files.

## Notes

This app does not scrape Nexus pages, bypass Nexus download restrictions, redistribute mods, or bundle mod archives. It uses Nexus API calls where available and falls back to the user's browser for downloads that require normal Nexus website handling.

Unsigned portable builds can trigger Windows Defender SmartScreen because Windows has not established publisher or file reputation for the executable. That warning cannot be suppressed from inside the app. For public distribution, Microsoft recommends Microsoft Store distribution or Artifact Signing/Trusted Signing for non-Store builds; SmartScreen reputation still builds over time from the signed publisher and release behavior, and EV certificates no longer automatically bypass SmartScreen reputation checks.
