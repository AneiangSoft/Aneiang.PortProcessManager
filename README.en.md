# PortProcessManager

<p align="center">
  <img src="PortProcessManager/icon.png" alt="Logo" width="100">
  <br>
  <strong>An efficient and intuitive Windows port and process association management tool</strong>
  <br>
  <a href="README.md">中文版</a> •
  <a href="#key-features">Key Features</a> •
  <a href="#environment">Environment</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#technical-implementation">Technical Implementation</a> •
  <a href="#notes">Notes</a>
</p>

---

[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-512bd4.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**PortProcessManager** is a lightweight tool designed for developers and system administrators. It monitors local network port usage in real-time and accurately associates it with corresponding process information, helping you quickly locate port conflicts, analyze abnormal connections, or clean up zombie processes with one click.

## ✨ Key Features

- **🔍 Comprehensive Scanning & Association**
  - Real-time listing of **TCP/UDP** port usage (IPv4).
  - Automatic resolution of process names, **full process paths**, running users, and **application icons**.
  - Real-time display of TCP connection states (LISTENING, ESTABLISHED, TIME_WAIT, etc.).

- **⚡ Efficient Operations**
  - **Fuzzy Search**: Instant filtering by port number, PID, process name, address, etc.
  - **Auto Refresh**: Optional 5-second interval to synchronize the latest network status.
  - **One-click Cleanup**: Support for ending individual processes, batch ending selected processes, or grouped cleanup by process path.
  - **Path Location**: Right-click to locate the executable in File Explorer.

- **🛡️ Intelligent Protection & Feedback**
  - **System Protection**: Built-in whitelist to automatically intercept termination requests for critical system services like `System`, `Idle`, `lsass`, etc.
  - **Closed-loop Detection**: Automatically tracks port status after ending a process to confirm release or transition to `TIME_WAIT`.

- **📊 View Optimization**
  - **Grouping Mode**: Aggregates connections by process to clearly see which ports a single program has opened.
  - **Change Highlighting**: Automatically highlights newly appeared connections or rows with state changes after a scan.

## 🖥️ Preview
![](/docs/ScreenShot_2026-02-12_122946_220.png)
![](/docs/ScreenShot_2026-02-12_122946_220.png)

## 🛠️ Technical Implementation

This project demonstrates best practices in modern WPF development:

- **Framework**: Built on `.NET 8.0`.
- **Architecture**: Strictly follows the **MVVM** pattern, utilizing `CommunityToolkit.Mvvm` for responsive data binding.
- **System Interfaces**:
  - Calls `iphlpapi.dll` (`GetExtendedTcpTable`/`GetExtendedUdpTable`) to get low-level network snapshots.
  - Calls `advapi32.dll` and `shell32.dll` to get process token user information and system icon mappings.
- **Dependency Injection**: Built-in simple service layer design, separating UI logic from system data sources.

## 🚀 Quick Start

### Option 1: Direct Run (Release)
Go to [Releases](#) and download the latest `PortProcessManager.Setup.msi` for installation.

### Option 2: Build from Source
1. Clone the repository:
   ```bash
   git clone https://github.com/YourUsername/PortProcessManager.git
   ```
2. Open `PortProcessManager.sln` using Visual Studio 2022.
3. Restore NuGet packages and build/run.

## ⚠️ Notes

1. **Permissions**:
   - To view process paths of other users or end protected processes, it is recommended to run the program as **Administrator**.
   - Some system process paths will show as `[Access Denied]`, which is normal behavior protected by the Windows kernel.
2. **Disclaimer**:
   - Please confirm the purpose of a process before ending it. Forcibly ending critical processes may lead to system instability or data loss.

## 📄 License

Licensed under the [MIT License](LICENSE).
