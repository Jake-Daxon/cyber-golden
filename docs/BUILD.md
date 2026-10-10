# 可复现构建与打包

Windows x64，在项目根目录打开 PowerShell。普通用户构建即可。

```powershell
./tools/bootstrap.ps1
./tools/package.ps1
```

bootstrap 只用于开发电脑下载固定 .NET SDK 10.0.401 和 Inno Setup 6.7.3。应用没有这些下载步骤。已准备好的 120 帧、音频和配置随源码存在，不需要再调用图像生成服务，也不需要 API Key。NuGet.Config 将包存到项目 tools/nuget；两个 packages.lock.json 记录 NuGet 版本，重复构建可以通过 `dotnet restore --locked-mode` 验证锁文件。

输出：`dist/CyberGolden-0.3.0-win-x64`、`dist/CyberGolden-Setup-0.3.0-x64.exe`、`dist/CyberGolden-Portable-0.3.0-x64.zip`、SHA256SUMS.txt。二进制内容、框架版本与素材可以复现；PE 时间戳及压缩元数据不保证跨环境逐字节相同。

`tools/package.ps1` 自包含 win-x64 发布，收集适用许可，在隔离存档路径运行实际 EXE 的自动验证，再编译安装 EXE、生成 ZIP 和 SHA-256。没有测试通过时不会正常完成脚本。`-SkipBuild` 可以复用已发布目录；`-SkipTests` 仅为开发调试参数，不代表交付经过测试。

实际安装/卸载：`./tools/verify-install.ps1`。脚本只把安装目录设为本项目 `verification/installed-app`，创建及清理一个明确的保留性测试标记，不覆盖真实存档，也不删除用户数据目录。它会实际创建和移除当前用户开始菜单快捷方式及当前用户卸载注册项。

真实进程重启：`./tools/verify-restarts.ps1`。分别用隔离存档启动睡眠、隐藏、暂停、静止基准及所选姿势七个进程（另含本轮饥饿和隐藏饥饿恢复）。

单独运行发布程序自测：

```powershell
./dist/CyberGolden-0.3.0-win-x64/CyberGolden.exe --self-test --data-dir ./test-data/manual --output ./verification/manual
```

源码中的 `--ui-test` 仅让宠物测试窗口出现在任务栏，便于 Windows 窗口工具选中。普通用户运行没有此参数，窗口不占任务栏。`--state-probe` 只用于重启验证，记录稳定行为后关闭。

素材整理：需要 Python 3.12+、Pillow、numpy、scipy、soundfile（开发用途）。可用 `./tools/bootstrap.ps1 -AssetTools` 安装到 tools/pythonlibs，再设置 `$env:PYTHONPATH='./tools/pythonlibs'`，运行 `python tools/prepare-assets.py` 与 `python tools/prepare-audio.py`。图像源是已保留的内置 imagegen 图集，整理脚本分帧、清理低 alpha 噪点、保持同动作统一缩放、对齐脚底和嘴部锚点，并生成真实 160 DIP 尺寸预览；没有把参考图的四个角度循环当作走路。声音处理是确定性的，来源及派生修改见 NOTICE。

原始上游 smoke 工程是可选的开发验证工程。若源码交付包没有 upstream 巨大图库，可从固定 SHA 的 codeload ZIP 恢复原始 Core 到 `upstream/VPet-main`；主要应用构建无需该目录，完整依赖的 Core 源码已在 src 中。
