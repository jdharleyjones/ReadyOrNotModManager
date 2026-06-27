# Release Notes

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
