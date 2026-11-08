# 架构

应用是 .NET Framework 4.8 的 WPF 桌面程序，使用 Win32 API 与 Windows 桌面集成。

`Program.cs` 负责入口、单实例和数据目录选择；普通版通过 `DesktopLaunchContext` 避免继承其他打包启动器的数据重定向，商店版由 `StorePlatform` 选择 LocalState。

`MainWindow` 的分部类承载分类、事项、步骤、外观与维护交互。`DesktopHost` 管理原生桌面父窗口及坐标转换；窗口底层源丢失由 `MainWindow.Lifetime` 重建并回收旧资源。

`MainWindow.Placement` 区分用户保存的位置和临时可见位置。日常数据保存、失去焦点或原生窗口销毁不会采样原生坐标覆盖偏好；用户完成拖动、缩放或主动恢复位置时提交几何。显示环境恢复后重试原位置。

`Storage` 使用 JSON、临时文件和替换操作保存状态；维护操作保留导入前快照。`CategoryPrivacy` 管理分类查看权限；它不对数据文件做加密。

`StartupTrace` 在本机临时目录记录有上限的诊断元数据，不记录事项正文、密码或草稿。发布和测试输出都保存在忽略目录。
