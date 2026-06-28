# Release Notes

## 1.3.7

- Add a bottom-left sidebar help button with a short in-app usage guide.
- Prefer `ReadyOrNotSteam-Win64-Shipping.exe` when extracting the local Ready or Not icon or resolving a direct launch fallback.

## 1.3.6

- Fix the Utilities modpack export dropdown so saved modpacks show their profile names instead of the `ModProfile` type name.
- Change the sidebar brand label from `RON` to `Ready Or Not`.
- Use the local Ready or Not executable icon for the app window/taskbar when available, with the bundled icon as fallback.

## 1.3.5

- Fix the Utilities modpack export dropdown so its disabled/empty state stays dark and transparent.
- Center the Settings page title and subtitle with the rest of the Settings controls.
- Show colour scheme display names instead of full `AppTheme` record text in dropdowns.

## 1.3.4

- Rename Extras to Utilities in the sidebar and page title.
- Redesign Utilities as a finished hub with modpack sharing, archive tools, cleanup, and planned utility cards.
- Add disabled Coming soon cards for Mod Conflict Scanner, Backup Deployed Mods, and Restore Previous Deployment.
- Disable modpack export controls and show a muted empty-state label when no saved modpacks exist.
- Move Settings to the bottom sidebar area and center Settings page controls.
- Add an `Input Profile Name` watermark to the Modpacks profile name field.

## 1.3.3

- Make the dashboard App Version icon/label clickable so users can retry the GitHub latest-release check.
- Rename Downloads to Extras in the sidebar and page title.
- Add an Extras page Import / Export Modpacks section above archive/download tools.
- Export saved modpacks as `.ronmodpack.json` files containing Nexus links and IDs only.
- Import shared modpack link files as local modpacks and load them into the Mods queue for download.
- Add tests for manual update retry gating and modpack share export/import safety.

## 1.3.2

- Rename the dashboard modpack action to **Show Local Modpacks**.
- Make **Show Local Modpacks** open the configured local modpack library folder in File Explorer and navigate to the in-app Modpacks page.
- Move the Nexus Mods and Nexus Collections dashboard buttons to the right action group while keeping Run Game furthest right.
- Add a `search mods` watermark to the Mods page search input.
- Force selected ComboBox text to use the current theme text brush so filter dropdown labels stay readable.

## 1.3.1

- Align dashboard card icons to the left of their labels and show connected Nexus accounts as a small `Correct:` label above the username.
- Restyle WPF dropdowns so closed and opened ComboBox controls match the dark tactical theme.
- Add URL input placeholder text and dashboard quick links for Ready or Not Nexus mods and collections.
- Rename sidebar Queue to Mods and Mods to Installed.
- Save and update modpacks from installed/deployed manifest records instead of stale queue rows.
- Document why unsigned portable builds may show Windows Defender SmartScreen warnings and how release signing addresses it.

## 1.3.0

- Redesign the WPF shell as a modern dark tactical operations dashboard.
- Add layered gradient background lighting, subtle radial glows, vignette shading, glassy cards, rounded panels, borders, and soft shadows.
- Improve sidebar spacing, hover states, icon alignment, and active navigation state.
- Redesign dashboard cards with icons, larger values, helper text, and structured recent activity rows.
- Split Settings into Nexus Connection, Game Folders, Appearance, Advanced, and Danger Zone cards.
- Add Queue toolbar search/status filtering, queue summary, dark table headers, hover rows, and coloured status badges.
- Add UI-only visual mapping helpers for queue statuses, recent activity, and dashboard status cards.

## 1.2.7

- Hide the progress bar until an operation reports progress.
- Add a Purple gradient colour scheme.
- Add a Settings checkbox to auto-test the Nexus API connection on app launch.
- Document that API keys, logs, queue data, settings, and manifests are local user data and are not shipped in GitHub release zips.

## 1.2.6

- Make Settings colour schemes visibly repaint the app instead of only saving the selected option.
- Add gradient shell, sidebar, panel, input, and table surface brushes for each theme.
- Add Dark mode and Light mode.
- Replace the ChatGPT theme option with a Codex palette.
- Keep Claude and Hacker palettes available.

## 1.2.5

- Fix a 1.2.4 startup crash where WPF theme brush resources could be frozen/read-only in the published executable.
- Add a regression test covering frozen theme brushes.

## 1.2.4

- Add Remove selected on the Queue page for accidental or duplicate queue entries.
- Move Run Game to the far right of the Dashboard quick actions panel.
- Add persistent colour themes: Tactical default, Claude, ChatGPT, and Hacker.
- Add right-click rename support for Modpacks.
- Add Dashboard update status showing the current version and latest GitHub release state.
- Add a polished WIP marker on the Downloads page.

## 1.2.3

- Add a Queue page Nexus URL input and Add Nexus URL button.
- Add semantic button colours for add/import/open, download, deploy, destructive, and modpack/special actions.
- Rename Dashboard deploy to Deploy all downloaded and make it deploy all archived queue items regardless of selected rows.
- Add Dashboard quick actions for Show Modpacks and Run Game.
- Add persistent recent activity based on status messages, errors, and deployed manifest records.
- Add a custom redistributable app icon for the released executable.

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
