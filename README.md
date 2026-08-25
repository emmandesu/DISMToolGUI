# DISM Tool GUI

[![Version](https://img.shields.io/badge/version-1.7.0--stable-0078d4)](https://github.com/emmandesu/DISMToolGUI/releases)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4)
![Framework](https://img.shields.io/badge/framework-.NET%20Framework%204.8-512bd4)

DISM Tool GUI is a Windows desktop front end for common **DISM** and **System File Checker** operations. It combines guided inputs, live output, image inspection, mounted-image management, and command previews in one responsive light/dark interface.

![DISM Tool GUI showing RestoreHealth and its command preview](docs/screenshots/restorehealth-command-preview.png)

> [!IMPORTANT]
> The application requests administrator privileges at startup because Windows servicing commands require elevation. Review the command preview before making changes to a system or image.

## Highlights

### Windows servicing

- Repair the running system or an offline Windows image with `RestoreHealth`.
- Mount and unmount WIM images with positive-index and empty-folder validation.
- Add CAB packages or remove installed packages from supported targets.
- List packages installed on the running Windows installation.
- Export an image index directly to another WIM file.
- Run `sfc /scannow` or the read-only `sfc /verifyonly` check.

| Mount a WIM image | Unmount and commit or discard |
|:---:|:---:|
| [![Mount WIM command and required fields](docs/screenshots/mount-wim-command-preview.png)](docs/screenshots/mount-wim-command-preview.png) | [![Unmount WIM modes and command preview](docs/screenshots/unmount-wim-command-preview.png)](docs/screenshots/unmount-wim-command-preview.png) |

| Add a CAB package | List installed packages |
|:---:|:---:|
| [![Add Package CAB command preview](docs/screenshots/add-package-command-preview.png)](docs/screenshots/add-package-command-preview.png) | [![Get Installed Packages command preview](docs/screenshots/get-packages-command-preview.png)](docs/screenshots/get-packages-command-preview.png) |

### WIM / ESD Image Inspector

Open **Tools → WIM / ESD Image Inspector** to:

- view every image index in a WIM or ESD file;
- compare edition name, description, architecture, version, and size;
- send the selected file and index back to the Mount or Export workflow;
- avoid guessing or manually entering image indexes.

### Mounted Image Manager

Open **Tools → Mounted Image Manager** to:

- refresh the list of mounted Windows images;
- view image file, index, access mode, mount directory, and status;
- open the selected mount directory in File Explorer;
- remount a recoverable image;
- commit or discard changes while unmounting;
- clean resources belonging to corrupted, unrecoverable mount points.

### Command safety

- Live preview updates as command fields and options change.
- Commands can be copied to the clipboard for review or reuse.
- Servicing changes require confirmation by default.
- Destructive mount actions use confirmation dialogs with the affected path.
- The main window cannot close while a servicing command is active.

### MSU Expander Tool

- Extract an `.msu` package to a selected folder.
- Optionally expand CAB payloads into `CAB_Extracted`.
- Keep CAB output separated by relative path to prevent name collisions.
- View progress and extraction output without freezing the window.

![Built-in MSU Expander Tool launched from the main window](docs/screenshots/msu-expander-tool.png)

## Quick start

1. Download the newest package from [GitHub Releases](https://github.com/emmandesu/DISMToolGUI/releases).
2. Extract the downloaded archive to a local folder.
3. Run `DismToolGui.exe` and approve the Windows UAC prompt.
4. Select an operation, complete the visible fields, and review **Command Preview**.
5. Select **Execute** and keep the application open until the operation finishes.

## Supported operations

| UI operation | Target | What it does |
|---|---|---|
| Run RestoreHealth | Online or offline | Repairs the component store; accepts an optional repair source |
| Mount WIM | Image file | Mounts a selected WIM index into an existing empty directory |
| Unmount WIM | Mounted image | Discards, commits, or commits and appends changes |
| Add Package (CAB) | Online or offline | Adds a selected CAB package |
| Get Installed Packages | Online | Lists packages installed on the running system |
| Remove Package | Online or offline | Removes a package by its DISM package identity |
| Export WIM | Image file | Exports a selected index directly to a destination WIM |
| MSU Expander Tool | Package file | Extracts MSU contents and optionally expands nested CAB payloads |
| SFC - Scannow | Online | Verifies protected system files and repairs detected problems |
| SFC - VerifyOnly | Online | Verifies protected system files without performing repairs |

## Common workflows

### Inspect and export an image

1. Open **Tools → WIM / ESD Image Inspector**.
2. Browse to the source image and select **Inspect**.
3. Select the required edition and choose **Use selected index**.
4. The main window opens the appropriate Mount or Export workflow with the image and index filled in.
5. Choose a destination WIM, review the preview, and execute the export.

### Service an offline image

1. Create an empty mount directory.
2. Select **Mount WIM**, provide the WIM file and index, and mount it.
3. Select a supported operation and choose **Offline (use Mount Folder)**.
4. When servicing is complete, open **Mounted Image Manager** or select **Unmount WIM**.
5. Choose **Commit** to save changes or **Discard** to abandon them.

### Repair Windows with a source image

1. Select **Run RestoreHealth**.
2. Choose the online system or an offline mount folder.
3. Optionally enter a repair source.
4. Review the generated DISM command and execute it.

When a repair source is supplied, the current workflow adds `/LimitAccess`, so DISM does not fall back to Windows Update.

## Requirements

- 64-bit Windows with DISM and SFC available
- .NET Framework 4.8
- Administrator privileges
- Enough free disk space for mounting, exporting, or expanding images and packages

## Safety notes

- Keep a backup of important WIM/ESD files before committing servicing changes.
- A mount directory must exist and be empty before mounting an image.
- **Commit** writes changes to the image; **Discard** permanently abandons uncommitted changes.
- Do not shut down Windows or terminate DISM while an operation is running.
- **Clean stale mounts** is intended for corrupted, unrecoverable mount points; healthy or recoverable mounts should be handled normally.
- Only download releases from this repository or another source you trust—the application runs elevated.

## Troubleshooting

| Problem | Suggested action |
|---|---|
| An image index is rejected | Use the Image Inspector and select an index from the detected list |
| A WIM will not mount | Confirm the file exists, the index is positive, and the mount directory is empty |
| A mounted image is inaccessible | Open Mounted Image Manager, refresh the list, and try **Remount** |
| RestoreHealth or package servicing fails | Review the live output and open `CBS.log` from the main window |
| Text is difficult to read | Switch between light and dark mode; existing log entries are recolored automatically |
| UAC appears every time | This is expected because the application manifest requires administrator privileges |

## Current limitations

- **Get Installed Packages** currently queries the running system only.
- Active servicing commands cannot be cancelled from the GUI; this avoids interrupting DISM during a critical write operation.
- The package installation workflow currently exposes CAB files, while MSU files are handled by the separate expansion tool.

## Feedback and issues

If you find a reproducible bug, open a [GitHub issue](https://github.com/emmandesu/DISMToolGUI/issues) and include:

- the selected operation;
- whether the target was online or offline;
- the generated command preview with sensitive paths removed;
- the DISM/SFC exit code and relevant log output;
- your Windows version and the application version.

Maintained by **Emmanuel Flores**.
