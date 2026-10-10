# 赛博金毛 CyberGolden

一只安静陪伴你的金毛幼犬，提供 Windows 桌面版与 Android 悬浮桌宠版。基于 [VPet](https://github.com/LorisYounger/VPet) Core 开发，透明无边框、本地离线运行。

<img src="previews/pose-FrontSit-160.png" width="160" height="160" alt="赛博金毛默认安静趴下，双爪落地" />

## 下载与安装

**普通用户无需编译代码。点击下面的安装包即可。**

| 文件 | 用途 |
|---|---|
| [下载 Android APK（测试版）](https://github.com/Jake-Daxon/cyber-golden/releases/download/android-v0.2.1-preview/CyberGolden-Android-0.2.1-preview.apk) | Android 8.0+：下载安装后开启悬浮窗权限；iPhone 无法安装 |
| [下载 Windows 安装 EXE](https://github.com/Jake-Daxon/cyber-golden/releases/download/v0.4.1-preview/CyberGolden-Setup-0.4.1-preview-x64.exe) | 推荐：双击安装，开始菜单、可选桌面快捷方式、卸载 |
| [下载便携 ZIP](https://github.com/Jake-Daxon/cyber-golden/releases/download/v0.4.1-preview/CyberGolden-Portable-0.4.1-preview-x64.zip) | 解压整个目录，运行 CyberGolden.exe |
| [逐步安装教程](docs/朋友安装教程.txt) | 先点哪个、后点哪个 |
| [全部版本与下载文件](https://github.com/Jake-Daxon/cyber-golden/releases) | Release 下载页、完整源码包和 SHA-256 |

面向 Windows 10/11 x64。安装包包含 .NET 运行环境、动画和声音，无需 Steam、原版 VPet、开发工具、账号或 API Key。

## Android 手机版

安卓版复用同一只金毛的动画和声音，默认安静趴下，支持食盆喂食、摸头、握手、睡觉、玩具、饥饿提醒、隐藏和保存。手机使用原生 Android 窗口与 Java 状态适配，Windows 版继续实际使用 VPet Core。安卓 APK 不包含 WPF/VPet DLL。

[安卓安装教程](docs/安卓安装教程.md) · [安卓源码与构建](android/README.md) · [安卓验证报告](docs/安卓验证报告.md)。

当前安卓版为 0.2.1-preview，长按悬浮菜单可上下滚动，提供完整动作；安装时直接覆盖旧版，无需卸载。

<img src="previews/android-main.png" width="280" alt="安卓 App 与悬浮金毛的真实模拟器截图" />

## 本次更新

Windows 0.4.1-preview / Android 0.2.1-preview：拖动时显示自然拎起姿势；饥饿每轮轻叫两声、前两次拖动释放后继续向中央移动；坐下观察和趴下休息延长尾帧；网球支持拾取、抛出、重力反弹和捡回。点击按头部、前爪、其他部位分配摸头、握手和随机动作，动作执行期间普通点击不打断。安静模式禁止叫声和自由漫步，漫游 CD 可调 5–1000 秒，拖动扣 2 点饱食度。

完整修改记录见 [版本说明](docs/本地体验版说明.md)。

## Windows 功能

- 普通待机默认安静趴下，双爪落地，不持续循环；坐姿、站姿、趴姿可选择并保存。
- 保留 CD 自主漫游、拖动、摸头、喂食、喝水、握手、睡觉/叫醒、网球和玩偶互动。
- 把普通本地空文件夹拖到狗身上即可投喂；文件夹不删除、不移动、不修改，可以重复使用。
- 喂食无间隔限制，饱腹也可继续喂；每次补 15、上限 100。普通主动互动扣 4，运行每小时扣 2，离线补算封顶。
- 低于 15 时持续显示“我饿了”；每轮到当前工作区中央提醒一次，达到 30 后解除。每轮仅轻叫两声，提醒声可关闭。
- 中文设置、托盘、Ctrl+Alt+D 隐藏/恢复、透明区域穿透、音量和静音、保存位置/大小/姿势/养成状态。

详细规则：[最新规则](docs/最新规则.md)。首次使用：[使用说明](docs/使用说明.md)。

## 构建

源码包含实际使用的 VPet Core 修改副本、121 个动作 PNG 帧（含拖动静态素材）、3 张静止姿势和本地声音。开发期在 Windows PowerShell 执行：

```powershell
./tools/bootstrap.ps1
./tools/package.ps1
```

构建工具下载固定 SDK 和 Inno Setup；最终用户程序没有首次启动下载。见 [可复现构建说明](docs/BUILD.md)。`dist/` 是本地构建产物，安装包在 GitHub Release 提供，不加入 Git 历史。

## 验证与素材说明

0.3.0 已通过 132 项自动检查、真实安装/启动/卸载、7 种独立进程恢复，并获得项目作者实际使用确认。干净虚拟机、完整多显示器/混合 DPI 等测试仍未全部完成，详见 [验证报告](docs/验证报告.md)。

角色使用授权参考生成的自然柔软 PNG，取消旧像素风。侧背面、尾巴和运动是合理补全；源关键帧仍有进一步精修空间。声音是授权狗叫派生和原创 Foley，非照片幼犬的实际声线。原始参考照片、个人存档、凭据和开发缓存不包含在仓库或安装包中。

## 许可与致谢

源码遵循 [Apache-2.0](LICENSE.txt)。基于 VPet 提交 `c3420bb77d6d00ed5fabf975a0945c4f6179bba3`，保留版权和修改声明。第三方及音频许可见 [NOTICE](docs/NOTICE.txt)、[许可清单](docs/THIRD-PARTY.md)；原始照片的授权范围不因源码许可而扩展。
