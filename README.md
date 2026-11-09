# TinyStar 桌面便签

![TinyStar](assets/shell-icon-preview/app-icon.png)

面向 Windows 10/11 的桌面便签与待办工具，使用 C#、WPF 和 .NET Framework 4.8 编写。当前源码版本为 **1.1.9**。

[下载 Windows 安装包](https://github.com/toggerdemetri-lgtm/tinystar-desktop-notes/releases/latest) · [微软商店](https://apps.microsoft.com/detail/9PFHSD4F4VV0)

## 功能

- 桌面常驻、窗口拖动与缩放、边缘吸附、位置锁定。
- 未完成、已完成、自定义分类，以及事项步骤、排序、回收站。
- 已完成事项的关键词与日期筛选。
- 可修改快捷键的快捷添加栏，支持保留输入草稿。
- 深浅主题、图片背景、独立透明度和界面大小调整。
- 分类密码、本地保存、导出备份与恢复。

1.1.9 修复长期运行时桌面图标刷新线程累积窗口注册资源的问题；保留此前的启动数据、闪烁光标和重启位置修复。

## 安装与数据

从 Releases 下载安装包，或通过微软商店安装。普通安装版和免安装版共用 `%LOCALAPPDATA%\ZhuomianNotes`；商店版保存在自身 LocalState 中，两者的数据相互独立。

升级前从托盘正常退出。迁移不同版本的数据时，先导出备份，再在目标版本恢复。卸载商店版前请导出备份。分类密码控制软件内的查看权限，本地数据文件与导出备份没有加密。

关闭窗口会隐藏到托盘；托盘菜单“退出”才会结束程序。默认快捷添加键为 `Ctrl+I`。

## 从源码构建

需要 Windows 10/11、.NET Framework 4.8，以及 PowerShell。构建使用系统自带的 .NET Framework 编译器，不依赖 NuGet 包。

```powershell
.\build.ps1 -Test
.\build.ps1
```

输出位于 `dist/1.1.9/`，包含安装包、免安装 ZIP、使用说明及 SHA-256 校验文件。

## 回归检查

测试使用独立的虚构数据目录。运行界面测试时，请保持 Windows 桌面处于解锁状态。

```powershell
.\tests\window-resources.ps1 -Rounds 150
.\tests\placement-recovery.ps1
.\tests\release-1.1.0-smoke.ps1 -ApplicationPath 'dist\1.1.9\ZhuomianNotes.exe' -ExpectedVersion '1.1.9.0'
.\tests\caret-rendering-probe.ps1 -ApplicationPath 'dist\1.1.9\ZhuomianNotes.exe' -Blink -DetachCaret
.\tests\RunWindowRecovery.ps1 -ApplicationPath 'dist\1.1.9\ZhuomianNotes.exe' -DisposedSource -HiddenWindow -EditingNote
```

## 可选 MSIX 构建

复制 `store/identity.example.json` 为 `store/identity.json`，填写你自己的商店预留身份信息，再使用官方 Windows SDK 的 MakeAppx：

```powershell
.\store\build-msix.ps1 -MakeAppx 'C:\path\to\Windows SDK\makeappx.exe'
```

MSIX 产物未签名，需要按你的分发方式签名或交由商店处理。仓库不包含发布凭据及本机商店后台配置。

## 目录

- `src/`：WPF 界面、本地存储、桌面集成、窗口恢复。
- `installer/`：当前用户安装器。
- `assets/`、`tools/IconBuilder.cs`：应用图标及生成工具。
- `tests/`：核心与界面回归检查。
- `store/`：商店平台适配及 MSIX 构建入口。
- `docs/`：使用说明及架构说明。

本地便签、数据备份、现场诊断、个人路径记录和生成目录不纳入版本控制。
