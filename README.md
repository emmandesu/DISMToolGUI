![image](https://github.com/user-attachments/assets/b6b5a4e6-4cba-4e67-aba9-27e93c54e622)

# 🛠️ DISM Tool GUI

**DISM Tool GUI** is a modern, user-friendly graphical interface for executing **DISM** and **SFC** commands on Windows. It allows IT professionals and power users to manage images, add/remove packages, run repairs, and monitor progress live — all without the command line.

![image](https://github.com/user-attachments/assets/a2cc6a40-5c5d-45de-9c72-150fc4b902ab)


---

## ✨ Features

- 🔹 Run `DISM` commands like:
  - RestoreHealth
  - Mount/Unmount WIM
  - Add or Remove Packages (CAB)
  - Extract MSU/CAB content
- 🔹 Run `SFC` (System File Checker)
- 🔹 Toggle between **Online** and **Offline** servicing modes
- 🔹 View **live logs** directly from the DISM output
- 🔹 Dark/Light theme switcher 🌙☀️
- 🔹 CBS.log quick access button
- 🔹 Clean and responsive UI using `System.Windows.Forms`

---

## 🚀 Getting Started

1. **Download** the latest release from the [Releases](https://github.com/emmandesu/DISMToolGUI/releases) page.
2. Extract the `.zip` and run `DismToolGui.exe`.
3. (Optional) Run as **Administrator** for full DISM capabilities.

> 📌 Requires **.NET Framework 4.7.2+**

---

## 🗂️ Supported Commands

| Command                | Online | Offline | Notes                           |
|------------------------|--------|---------|---------------------------------|
| RestoreHealth          | ✅     | ✅      | Source path supported           |
| Mount WIM              | ❌     | ✅      | Requires WIM + Index + Folder   |
| Unmount WIM            | ❌     | ✅      | With discard/commit/append      |
| Add Package (CAB)      | ✅     | ✅      | MSU supported via extraction    |
| Remove Package         | ✅     | ❌      | Requires package name           |
| Get Installed Packages | ✅     | ❌      | DISM `/Get-Packages`            |
| Mount and Export       | ❌     | ✅      | Mounts, exports, then unmounts  |
| Extract MSU/CAB        | ✅     | ✅      | Uses `expand.exe`               |
| SFC Scannow            | ✅     | ❌      | Native system file scan         |
| SFC VerifyOnly         | ✅     | ❌      | Scan-only                       |

---

## 📦 Building from Source

1. Clone the repo:
   ```bash
   git clone https://github.com/yourusername/dism-tool-gui.git
