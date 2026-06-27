# Release Notes

## 1.2.2

- Fix deployment identity so multiple selected manual/imported archives no longer replace each other.
- Fix multiple Nexus files from the same mod page deploying as separate selected queue items.
- Run deployment extraction on background work so the progress bar can update while archives are copied.
- Enlarge the bottom progress bar and make current operation text easier to read.
- Move game/Nexus connection status into the sidebar with clear connected, unknown, and disconnected icons.
- Remove the top-right Add Nexus URL and Settings shortcuts from the shell header.
- Improve table header contrast across queue, modpack, log, and archive selection tables.

## 1.2.1

- Allow Import Archive to select and import multiple archive files in one dialog.
- Extra selected archives are added as manual queue rows while a selected queue row receives the first archive.

## 1.2.0

- Replace the setup-heavy sidebar with a first-time setup wizard and a professional navigation shell.
- Add Dashboard, Mods, Queue, Modpacks, Downloads, Settings, and Logs/Errors pages.
- Add visual setup validation, Nexus API key testing, and Ready or Not Steam folder auto-detection.
- Move API key and folder configuration into Settings, with a Reset setup wizard option.
- Add dashboard summary cards for game status, Nexus status, installed mod count, pending queue count, and recent activity.
- Add bundled WPF icons through MahApps.Metro.IconPacks so the app works offline after install.
- Add subtle page transition and button hover styling.

## 1.1.0

- Add an Advanced options toggle for choosing which `.pak` groups deploy when an archive contains multiple variants.
- Add local Modpacks profiles for saving, loading, updating, deleting, and activating switchable mod setups.
- Add a separate modpack library folder setting for copied profile archives and profile metadata.
- Add overall progress reporting for download and deployment passes.
- Add persistent error logging with an Errors window for failed downloads/deployments and manual-fix helper actions.
- Continue queue operations after per-item download or deployment failures.
- Track profile IDs and selected archive entries in the install manifest.

## 1.0.1

- Fix deployment for 7z archives by using SharpCompress archive handling instead of reader-only handling.
- Add `.7zip` as an accepted import extension alias.
- Improve unsupported archive errors so they name supported extensions: `.zip`, `.rar`, `.7z`, and `.7zip`.

## 1.0.0

- Initial Windows desktop build for Ready or Not Nexus mod management.
- Add individual Ready or Not Nexus mod URLs and collection URLs.
- Expand Nexus collections through the official GraphQL API.
- Download mod archives through Nexus-supported API flows, with browser/import fallback.
- Support ZIP, RAR, and 7z archive detection and deployment.
- Deploy Unreal mod files into the Ready or Not `ReadyOrNot\Content\Paks` folder.
- Track deployed files in a local manifest for uninstall.
- Delete downloaded archive files separately from uninstalling deployed files.
- Clear local user data, including saved API key, selected folders, install manifest, and current queue.
