# 第三方组件与素材

| 组件 | 采用版本 | 许可 / 声明 |
|---|---|---|
| VPet-Simulator.Core | c3420bb77d6d00ed5fabf975a0945c4f6179bba3 | Apache-2.0；保留原始源码和修改标记 |
| LinePutScript | 1.11.9 | 包内 LICENSE（Apache-2.0）；LB Copyright ©2018 |
| LinePutScript.Localization.WPF | 1.0.7 | 包内 LICENSE（Apache-2.0）；exLB.org ©2023 |
| Panuon.WPF | 1.1.3 | Apache-2.0；Panuon Group ©2018–2022 |
| Panuon.WPF.UI | 1.2.4.10 | Apache-2.0；Panuon ©2018–2024 |
| SkiaSharp / native Windows Skia | 3.116.1 | MIT 及包内 THIRD-PARTY-NOTICES；Xamarin/Microsoft 等 |
| Microsoft .NET / Windows Desktop | 10.0.12 自包含 | 随 runtime 的 MIT LICENSE 和 THIRD-PARTY-NOTICES |
| Inno Setup（仅安装器） | 6.7.3 | LICENSE-Inno.txt，Jordan Russell / Martijn Laan 等 |
| 简体中文安装翻译 | is-6_7_3 官方源码分支 | 保留 tools/ChineseSimplified.isl 中 Zhenghan Yang / Kira 署名 |
| click / happy 狗叫 | Amada44 的 Barking of a dog.ogg 派生 | CC BY-SA 3.0，作者、原页面、许可和修改均见 NOTICE |
| eat / drink / breath Foley | 本项目确定性程序素材 | CC0；不是幼犬实际录音 |
| 狗角色动作 | 内置 imagegen，用户照片及 PNG 身份参考 | 推断侧背面、尾巴和动态；没有分发原始照片 |
| 围兜、图标和玩具 | 用户角色衍生 / 项目绘制 | 安装只包含必要衍生 PNG 和原生玩具形状 |

完整包许可及 NuGet metadata 收集在可运行目录 `licenses`。没有打包第三方字体，中文使用 Windows 已安装字体。原始参考照片和用户真实数据仅在开发目录，未进入分发包。

开发期 Python/Pillow/numpy/scipy/soundfile 不属于最终运行程序依赖，朋友无需安装它们。开发 SDK、NuGet 缓存和 Inno 编译器不打入便携包。
