# PortProcessManager

<p align="center">
  <img src="PortProcessManager/icon.png" alt="Logo" width="100">
  <br>
  <strong>一个高效、直观的 Windows 端口与进程关联管理工具</strong>
  <br>
  <a href="#主要功能">主要功能</a> •
  <a href="#运行环境">运行环境</a> •
  <a href="#快速开始">快速开始</a> •
  <a href="#技术实现">技术实现</a> •
  <a href="#注意事项">注意事项</a>
</p>

---

[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-512bd4.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**PortProcessManager** 是专为开发者和系统管理员设计的轻量级工具。它能够实时监控本地网络端口占用情况，并精准关联到对应的进程信息，帮助你快速定位端口冲突、分析异常连接或一键清理僵尸进程。

## ✨ 主要功能

- **🔍 全能扫描与关联**
  - 实时列出 **TCP/UDP** 端口占用（IPv4）。
  - 自动解析进程名、**进程完整路径**、运行用户以及**应用程序图标**。
  - 实时显示 TCP 连接状态（LISTENING, ESTABLISHED, TIME_WAIT 等）。

- **⚡ 高效操作**
  - **模糊搜索**：支持按端口号、PID、进程名、地址等关键字即时筛选。
  - **自动刷新**：可选 5 秒间隔自动同步最新网络状态。
  - **一键清理**：支持结束单个进程、批量结束选中进程、或按进程路径进行分组清理。
  - **路径定位**：右键即可在资源管理器中定位可执行文件。

- **🛡️ 智能保护与反馈**
  - **系统保护**：内置白名单，自动拦截针对 `System`、`Idle`、`lsass` 等关键系统服务的结束请求。
  - **闭环检测**：结束进程后自动追踪端口状态，确认是否真正释放或进入 `TIME_WAIT`。

- **📊 视图优化**
  - **分组模式**：按进程聚合连接，清晰查看同一程序开启了哪些端口。
  - **变更高亮**：扫描后自动高亮新出现的连接或状态发生变化的行。

## 🖥️ 运行预览
![](/docs/ScreenShot_2026-02-12_122946_220.png)
![](/docs/ScreenShot_2026-02-12_122946_220.png)

## 🛠️ 技术实现

该项目展示了现代 WPF 开发的最佳实践：

- **框架**：基于 `.NET 8.0` 构建。
- **架构**：严格遵循 **MVVM** 模式，利用 `CommunityToolkit.Mvvm` 实现响应式数据绑定。
- **系统接口**：
  - 调用 `iphlpapi.dll` (GetExtendedTcpTable/GetExtendedUdpTable) 获取底层网络快照。
  - 调用 `advapi32.dll` 与 `shell32.dll` 获取进程令牌用户信息及系统图标映射。
- **依赖注入**：内置简单的服务层设计，分离 UI 逻辑与系统数据源。

## 🚀 快速开始

### 方式一：直接运行（发布版）
前往 [Releases](#) 下载最新的 `PortProcessManager.Setup.msi` 进行安装。

### 方式二：从源码编译
1. 克隆仓库：
   ```bash
   git clone https://github.com/YourUsername/PortProcessManager.git
   ```
2. 使用 Visual Studio 2022 打开 `PortProcessManager.sln`。
3. 还原 NuGet 包并编译运行。

## ⚠️ 注意事项

1. **权限要求**：
   - 查看其他用户的进程路径或结束受保护进程时，建议以**管理员身份**运行本程序。
   - 部分系统进程路径会显示为 `[Access Denied]`，这是受 Windows 内核保护的正常现象。
2. **免责声明**：
   - 结束进程前请确认该进程的作用，强行结束关键进程可能导致系统不稳定或数据丢失。

## 📄 开源协议

根据 [MIT License](LICENSE) 许可授权。
