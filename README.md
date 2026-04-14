# 🛠️ DISM Tool GUI

**DISM Tool GUI** is a modern, user-friendly Windows desktop application for running **DISM** and **SFC** operations without relying on the command line. It is designed for IT professionals, system administrators, and power users who need a faster and more convenient way to service Windows images, install or remove packages, repair system health, and monitor command output in real time.

![DISM Tool GUI Screenshot](https://github.com/user-attachments/assets/a2cc6a40-5c5d-45de-9c72-150fc4b902ab)

---

## ✨ Overview

Managing Windows image servicing tasks through Command Prompt can be repetitive and error-prone. **DISM Tool GUI** provides a cleaner graphical interface that helps you perform common maintenance operations with less friction.

With this tool, you can:

- run common **DISM** repair and servicing tasks
- execute **SFC** commands quickly
- work with both **online** and **offline** images for supported operations
- view live command output directly in the application
- switch between **dark** and **light** themes
- launch the built-in **MSU Expander Tool** for update package extraction

---

## 🔧 Features

- **DISM Operations**
  - RestoreHealth
  - Mount WIM
  - Unmount WIM
  - Add Package (CAB)
  - Remove Package
  - Get Installed Packages
  - Mount and Export

- **SFC Operations**
  - `sfc /scannow`
  - `sfc /verifyonly`

- **MSU Expander Tool**
  - Expand `.msu` packages into a chosen destination
  - Optional **Deep Expand CAB Payloads** mode
  - Built-in progress and logging

- **Live Logging**
  - Streams command output directly into the app
  - Makes it easier to monitor progress and errors in real time

- **Online / Offline Servicing**
  - Supports online and offline image servicing for supported commands

- **UI Quality of Life**
  - Dark / Light theme switcher
  - Quick access to `CBS.log`
  - Clean Windows Forms interface
  - Simple workflow for common DISM servicing tasks

---

## 📋 Supported Commands

| Command                | Online | Offline | Notes |
|------------------------|:------:|:-------:|-------|
| RestoreHealth          |   ✅   |   ✅    | Supports optional source path |
| Mount WIM              |   ❌   |   ✅    | Requires WIM path, index, and mount folder |
| Unmount WIM            |   ❌   |   ✅    | Supports discard, commit, and append |
| Add Package (CAB)      |   ✅   |   ✅    | CAB package installation |
| Remove Package         |   ✅   |   ✅    | Requires package name |
| Get Installed Packages |   ✅   |   ❌    | Uses DISM `/Get-Packages` |
| Mount and Export       |   ❌   |   ✅    | Mounts image, exports, then unmounts |
| MSU Expander Tool      |   ✅   |   ✅    | Opens dedicated package expansion tool |
| SFC Scannow            |   ✅   |   ❌    | Full system file integrity scan |
| SFC VerifyOnly         |   ✅   |   ❌    | Verification-only scan |

---

## 📦 Getting Started

1. Download the latest release from the [Releases](https://github.com/emmandesu/DISMToolGUI/releases) page.
2. Extract the downloaded archive.
3. Run `DismToolGui.exe`.
4. For best results, launch the application as **Administrator**.

> **Note:** Some DISM and SFC operations require elevated privileges.

---

## 🖥️ Requirements

- Windows system with **DISM** and **SFC** available
- **.NET Framework 4.8** runtime for the provided `net48` build
- Administrator privileges for most servicing and repair operations

---

## 🏗️ Building from Source

1. Clone the repository:

   ```bash
   git clone https://github.com/emmandesu/DISMToolGUI.git
