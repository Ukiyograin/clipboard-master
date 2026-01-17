# Clipboard Master 
**一个现代化、高性能的 Windows 剪贴板管理器**

## 📋 项目状态

> ⚠️ **重要通知** ⚠️
> 
> **项目开发进度缓慢**
> 
> 这个项目由于以下原因，开发进展相对缓慢：
> 
> 1. **复杂度高** - 项目涉及多语言混合开发（Rust + C#），系统级API调用，内存管理复杂
> 2. **Windows系统集成** - 需要处理剪贴板监控、全局热键、系统托盘等底层功能
> 3. **性能要求** - 需要低内存占用、高响应速度，优化工作量大
> 4. **数据安全** - 需要处理敏感数据存储和加密
> 5. **用户体验** - 需要精致的UI设计和流畅的交互
> 
> 目前进度：约 40%
> 预计完成时间：未知

## 🚀 项目概述

Clipboard Master 是一个功能强大的 Windows 剪贴板管理器，旨在提供超越系统默认剪贴板的增强功能。

## ✨ 核心特性

### 🎯 核心功能
- ✅ 文本、图片、文件、HTML 剪贴板内容支持
- ✅ 持久化存储（重启不丢失）
- ✅ 全局快捷键支持（Ctrl+Shift+V 等）
- ✅ 系统托盘集成
- ✅ 智能搜索和过滤
- ✅ 标签分类系统

### 🎨 用户界面
- ✅ 现代化的 WinUI 3 界面
- ✅ 深色/浅色主题
- ✅ 响应式设计
- ✅ 动画和过渡效果
- ✅ 缩略图预览

### ⚡ 性能优化
- ✅ 低内存占用（< 50MB）
- ✅ 快速搜索和过滤
- ✅ 智能缓存机制
- ✅ 数据库优化（SQLite + WAL）

### 🔒 数据管理
- ✅ 自动归档和清理
- ✅ 数据导入/导出
- ✅ 备份和恢复
- ✅ 可选的数据加密

## 🛠️ 技术栈

### 后端（核心）
- **语言**: Rust
- **功能**: 剪贴板监控、数据处理、数据库操作
- **库**: windows-rs, rusqlite, image, serde

### 前端（UI）
- **语言**: C#
- **框架**: WinUI 3 (.NET 8)
- **功能**: 用户界面、系统集成、热键管理

### 数据存储
- **数据库**: SQLite
- **缓存**: 内存 + 磁盘缓存
- **配置**: JSON 配置文件

## 📁 项目结构
```
ClipboardMaster/
├── src/
│ ├── ClipboardMaster.Core/ # Rust核心库
│ │ ├── src/
│ │ │ ├── lib.rs # 核心逻辑
│ │ │ ├── clipboard_monitor.rs # 剪贴板监控
│ │ │ ├── database.rs # 数据库操作
│ │ │ ├── image_processor.rs # 图片处理
│ │ │ └── ffi.rs # C接口导出
│ │ └── Cargo.toml
│ │
│ ├── ClipboardMaster.Backend/ # Rust后端服务
│ │ └── Cargo.toml
│ │
│ ├── ClipboardMaster.UI/ # C# WinUI 3前端
│ │ ├── Views/ # 界面视图
│ │ ├── ViewModels/ # 视图模型
│ │ ├── Services/ # 业务服务
│ │ ├── Models/ # 数据模型
│ │ ├── Controls/ # 自定义控件
│ │ ├── Converters/ # 数据转换器
│ │ └── ClipboardMaster.UI.csproj
│ │
│ └── ClipboardMaster.Tray/ # C#托盘组件
│ └── ClipboardMaster.Tray.csproj
│
├── assets/ # 资源文件
│ ├── icons/ # 图标
│ ├── sounds/ # 音效
│ └── fonts/ # 字体
│
├── data/ # 用户数据
│ ├── database/ # 数据库文件
│ ├── cache/ # 缓存文件
│ └── logs/ # 日志文件
│
├── tests/ # 测试文件
├── build.ps1 # PowerShell构建脚本
├── run.bat # 运行脚本
├── appsettings.json # 配置文件
└── README.md # 本文档
```

## 🔧 安装和构建

### 系统要求
- **操作系统**: Windows 10/11 (1809+)
- **运行时**: .NET 8.0 Runtime
- **内存**: 4GB RAM (推荐 8GB)
- **存储**: 500MB 可用空间

### 构建步骤

1. **安装依赖**
```powershell```
# 安装 Rust 工具链
```winget install --id Rustlang.Rustup```
# 安装 .NET 8.0 SDK
```winget install Microsoft.DotNet.SDK.8```
# 安装 Windows SDK
```winget install Microsoft.WindowsSDK```
# 克隆项目
```
bash
git clone https://github.com/yourusername/clipboard-master.git
cd clipboard-master
```
### 🤝 贡献指南
**由于项目复杂且开发缓慢，欢迎贡献！**

- 贡献步骤
```Fork 项目
- 创建功能分支 (git checkout -b feature/AmazingFeature)
- 提交更改 (git commit -m 'Add some AmazingFeature')
- 推送到分支 (git push origin feature/AmazingFeature)
- Pull Request
```

### 开发规范
```
代码风格: Rust使用rustfmt，C#使用.editorconfig
提交信息: 遵循Conventional Commits规范
文档: 所有公共API需要文档注释
测试: 新功能需要包含测试用例
```

---

> ⚠️ **免责声明**：
> - 本项目仅供学习和研究使用
> - 作者不对使用本项目造成的任何损失负责
> - 使用本软件即表示您同意自行承担所有风险
