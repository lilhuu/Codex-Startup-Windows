# 开发、验证与发布

## 启动链路

`CodexStartup.exe` → 隐藏的系统 Windows PowerShell → 查找并校验官方 MSIX 包 → Windows 包激活 → 随机回环 CDP 端口 → `extension/windows.mjs` 校验监听与目标页面 → 临时 iframe 播放动画 → 移除覆盖层，保留壁纸和设置快捷键。

设置入口传入 `--settings`。首次启动时直接打开设置；已有本项目会话则读取本用户 `session.json` 的端口，并重新校验监听身份，复用当前扩展。普通官方会话没有项目端口时提示退出，不自动杀进程。

安装 EXE 内嵌运行时 ZIP。`InstallCore.cs` 先解包到同一父目录的暂存文件夹，检查路径边界，再用原子文件替换提交。失败反向恢复；成功移除备份。所有文件都记录到所有权清单，更新和卸载保留无关文件。当前用户桌面、开始菜单与卸载注册信息由 `InstallIntegration.cs` 管理。

## 用户数据

图片与文字位于官方主页面 origin 的 IndexedDB：`codex-startup-windows-images`。它与原项目及本机个人启动器的设置空间分开。浏览器数据目录隔离不代表官方账户状态隔离，不在源码、截图或发布包中保存账户资料。

日志与当前接入记录在 `%LOCALAPPDATA%\CodexStartupWindows`，安装程序在其 `app` 子目录。`session.json` 只是启动定位信息，不能跳过监听校验。

## 本机验证

完整运行验证结果记录在 `docs/screenshots/windows-verification.json`。安装测试覆盖首次安装、更新、回滚、归属判断、路径穿越拒绝、卸载数据保留。集成检查使用真实安装器内嵌 ZIP，在含中文与空格的目录创建并检查快捷方式、注册测试卸载键，再清理。

实际窗口验证使用官方 MSIX `26.930.4958.0`、包内应用 `26.930.41038`，并指定独立诊断浏览器数据目录。验证内容包括直接进入设置、默认动画、设置会话复用、自定义头像/静态背景/文字/GIF、持久化、默认恢复、跳过与外观恢复。此结果不代表其他机器或版本已通过实机验证。

真实窗口测试需先用诊断目录从运行包的 `windows/launch.ps1 -ProfileDirectory <绝对路径> -OpenSettings -NoDialog` 启动，再执行：

```powershell
node tools/windows.smoke.mjs <端口> docs/screenshots <测试GIF路径> <诊断浏览器目录>
```

该脚本会修改被指定诊断窗口的图片设置，不应对用户的主工作窗口运行。测试后仅关闭这次诊断进程，不能关闭正常主进程。

## 发布

1. 修改 `project.json` 与 `InstallCore.Version` 的版本号，更新构建脚本中的一致性检查。
2. 执行 Node 与安装器测试，构建 release，再运行真实嵌入 ZIP 集成测试。
3. 需要兼容性验证时在诊断窗口测试，保留明确的测试版本和限制。
4. 把安装 EXE、便携 ZIP、`SHA256SUMS.txt`、说明文件作为 GitHub Release 附件。
5. 安装器若要签名，应由发布者使用自己的代码签名证书；项目没有附带证书或凭据。

GitHub 工作流仅构建和上传测试产物，不自动发布 Release，不含访问令牌。
