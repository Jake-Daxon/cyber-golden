# 赛博金毛 Android

原生离线安卓 App，Android 8.0 / API 26 及以上。复用同仓库 Windows 版的 120 张连续帧、3 个静止姿势、5 个本地声音及饱食度/饥饿规则。WPF 与 VPet DLL 只能运行在 Windows；这里使用 Java/Android 实现显示与状态适配，未声称直接运行 VPet 核心程序集。Windows 版保持不变。

构建：安装 JDK 17+，在 Windows PowerShell 中运行 `tools/bootstrap-android.ps1`，然后 `tools/build-android.ps1`。也可分别用 `-JavaHome` / `-SdkRoot` 指定已有工具。素材脚本需 Python 3 + Pillow。构建不依赖 Gradle 或第三方安卓库，使用 Google 官方 SDK 36 / Build Tools 36.0.0 的 javac、aapt2、D8、zipalign、apksigner。使用 `android/tests/PetStateTest.java` 验证状态机。

APK：`dist/CyberGolden-Android-0.1.0.apk`。第一次构建在本机 `.android-private` 生成发行签名密钥。后续版本复用该目录的密钥才能覆盖更新；密钥和密码不进入源码、APK、ZIP 或 GitHub。源码和工具可复现构建流程，不分发私人签名密钥。

手机安装和限制见 `docs/安卓安装教程.md`；已验证范围见 `docs/安卓验证报告.md`。
