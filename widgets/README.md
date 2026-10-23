# 原版 iCUE 传感器小组件 · 2026-10-23

独立的 Windows 11 Win+W 小组件，读取正在运行的官方 iCUE；无需运行 iCUE Next Demo，也不替换官方程序。15:23 界面 Demo 的原发布包保持不变。本组件为个人实验项目，非 CORSAIR 官方产品。

## 本次修正

- 异步启动，传感器未就绪时快速重试；连接中断后重新连接。
- 首次显示最多等待约 1.8 秒，让选中的温度和至少两个真实曲线采样准备好，减少“未提供 → 数据和曲线”的闪变；后台启动期间保留宿主现有卡片。
- 设备长期不可用时仍显示连接提示，不伪造温度。启动首分钟每秒更新，此后约每 5 秒更新。
- 双击卡片或点击“打开 iCUE 原版”打开官方程序。

已验证真实 iCUE 首次读取、服务延迟就绪、断线恢复，以及选择测点、隐藏曲线、过期数据和首帧等待测试。已安装版冷启动测试约 2.703 秒推送三项温度和三条曲线。该数字是本机单次测试结果，不是所有机器的保证；完整 Windows 重启及其他设备组合未做通用验证。

## 安装与更新

需要 Windows 11 x64（22621 或更新）和可用的 Windows 小组件面板。测试使用官方 iCUE 5.52.93、H100i ELITE LCD 与 NVIDIA 显卡。读取依赖 iCUE 的私有本地传感器接口，未来版本或其他设备可能不兼容。官方 iCUE 必须保持运行；本组件不会结束或重启它。

1. 下载并解压本次 Release 的 ZIP，按 `SHA256SUMS.txt` 核对文件。
2. 包使用自签名证书 `CN=CueReplicaPreview`。公开证书为 `CueWidgets-Public.cer`，SHA-1 指纹为 `5FC9E3DB5309FA67685840837E02DC65CC4821D1`。它仅含公钥，不含私钥。
3. 首次安装者：确认来源和指纹后，手动打开证书，使用 Windows 证书安装向导将它放入“本地计算机 → 受信任人”。Windows 可能要求管理员确认。不要将其导入“受信任的根证书颁发机构”。若不希望信任此证书，可以自行编译并签名。安装脚本不会自动导入证书或更改安全设置。
4. 在当前用户的 PowerShell 中运行 `powershell -NoProfile -File .\Install.ps1`。脚本先检查签名，自动选择旧卡片兼容包（若已安装 `CueReplica.WindowsWidgets`），否则安装新身份包。已有卡片通常可保留；如果面板未刷新，关闭并重新打开 Win+W。
5. 首次使用：Win+W 添加“iCUE 原版 传感器”。不要同时安装两个身份，除非确实需要两组卡片。

主包版本 `CueOfficial.WindowsWidgets 1.0.0.5`；兼容旧 Next 卡片的包为 `CueReplica.WindowsWidgets 1.0.0.13`。兼容包的用途是更新原来固定的卡片，显示和操作均面向官方 iCUE。

## 温度和曲线设置

已安装外观补丁的用户可继续使用“一般设置”中的选项。本发布不包含外观补丁或官方 DLL。

独立使用者可将 `settings.example.json` 复制为 `%LOCALAPPDATA%\Corsair\iCUEAppearancePatch\widgets.json`。已有该文件时只修改需要的字段，避免覆盖现有设置。更改会在下一次刷新读取，无需重启。

- `widgetSensor0/1/2`：`CPU 温度`、`GPU 温度`、`冷却液温度`；`none` 隐藏此行。
- `widgetCharts`：`1` 显示真实近期采样曲线，`0` 隐藏。
- `winWidgetsEnabled`：`1` 更新、`0` 暂停；`fahrenheit`：`1` 华氏、`0` 摄氏。

未配置时默认三项温度、隐藏曲线。设备没有对应测点时会显示未提供。图表按近期数据自动缩放，不是从零开始的固定温度轴。

## 移除与本地文件

从 Windows“已安装的应用”卸载对应小组件，或运行 `Get-AppxPackage CueOfficial.WindowsWidgets | Remove-AppxPackage`（旧卡片包改用 `CueReplica.WindowsWidgets`）。不影响官方 iCUE 或 Demo。

本地快照、最近卡片和日志位于 `%PROGRAMDATA%\CueOfficialWidgets\<当前用户 SID>`，兼容包使用其中 `MigratedCard` 子目录。设置位于上述 LocalAppData JSON。卸载不主动清除这些文件。数据来自本机，不上传到外部服务。

## 从源码构建

源码位于本仓库 `widgets/`。需要 .NET 8 SDK、Windows 10/11 SDK（MakeAppx、SignTool）、Python 3.12 x64。

```powershell
powershell -NoProfile -File .\widgets\build.ps1
# 旧固定卡片兼容身份：加 -Migration
MakeAppx pack /d .\widgets\dist /p .\CueOfficial.WindowsWidgets.msix /o
# 使用自己的证书签名；Manifest 的 Publisher 须与证书 Subject 一致
SignTool sign /fd SHA256 /sha1 YOUR_CERT_THUMBPRINT .\CueOfficial.WindowsWidgets.msix
```

运行 `widgets\dist\CueWidgets.exe --self-test` 可执行首帧及曲线测试；测试会生成 JSON/TXT，打包前移除 `card-test.json`、`widget-tests.txt`。测试入口不会返回详细失败退出码，应核对 `widget-tests.txt` 的 PASS 内容与本地日志。

Qt/PySide 以独立动态库随传感器桥分发，可以替换库或从公开的 `bridge.py` 重新构建。允许为调试这些库的修改而逆向工程；不限制 LGPL 赋予的权利。许可证与依赖来源见 [THIRD_PARTY.md](THIRD_PARTY.md)。不包含任何作者私有配置、传感器快照或私钥。
