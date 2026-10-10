# VPet 上游检查记录

采用 https://github.com/LorisYounger/VPet 的 main 提交 `c3420bb77d6d00ed5fabf975a0945c4f6179bba3`。获取日期：2026-10-10（北京时间）。Git ls-remote 与 GitHub 源码 ZIP comment 均返回同一 SHA。首次 git clone 写入 refs 失败，改为源码 ZIP；原始副本保留在 `upstream/VPet-main`，应用实际引用的是 `src/VPet-Simulator.Core` 的修改副本。

已完整阅读需求三文件及四张参考图；本次用户指定的“照片 → 主 PNG → 多视图 PNG → 文字”优先级覆盖 ASSET_SPEC 旧排序及尚无 PNG 的旧说明。当前工作树和上游没有找到适用的 AGENTS.md。

原始 Core：`net8.0-windows7.0`，WPF + Windows Forms，依赖 LinePutScript 1.11.9、Localization.WPF 1.0.7、Panuon.WPF 1.1.3、Panuon.WPF.UI 1.2.4.10、SkiaSharp 3.116.1。原始 Windows 宿主为 `net10.0-windows7.0`，有 SteamClient.Init、工坊、云存档和 Steam 遥测入口；本项目不引用该宿主或 Windows.Interface。

已编译原始 Core，并通过 `src/UpstreamSmoke` 的 WPF 宿主实际运行其 PetLoader、PNGAnimation、GameSave，加载 20 组金毛资源并验证 LPS 存档往返（`verification/upstream/upstream-smoke.json`）。这不是宣称完整原版 Windows/Steam 程序已经运行；完整原版宿主未启动，避免触发与本项目无关的 Steam/网络初始化。

最终宿主真实复用：GameCore 持有 GraphCore 与 GameSave；PetLoader 递归读取资源；GraphInfo 按 info.lps 确定名称/类型/模式；PNGAnimation 真实播放连续 PNG 帧；GameSave 使用原有五项属性、变化接口、CalMode 及 ToLine/Load 持久化。没有重新实现一个冒充 VPet 的独立图片播放器。

加载规则：叶目录包含多个 PNG 时由 PNGAnimation 加载；末尾 `_<毫秒>.png` 解析帧时长。info.lps 显式指定 `pnganimation#动作名:|graph#类型:|mode#Nomal:|animat#Single:|loop#false:|`。Host 可以按持续行为切换 IsLoop。名称来自配置，避免路径推断混淆。帧按序号排序。

本机原来只有 .NET 10.0.12 运行时，没有 SDK。本次在项目 tools 下安装 .NET SDK 10.0.401，并将副本 Core retarget 到 net10.0-windows，使最终发布使用统一的 .NET 10 Windows Desktop 自包含运行时。关闭裁剪、单文件、CETCompat；所有素材为实体文件。无需 SDK/Python/Steam 在朋友电脑上运行。

Core 修改：确定 PNG 排序；预解码图集；帧显示事件供透明命中测试；在渲染前及回调前阻止已取消或被替换的旧任务；图集缓存移到用户数据目录；保留原始版权、Apache-2.0 及修改标记。取消由宿主与 Core 双重代次检查实现。

宿主采用单一状态机统一声音、玩具和自主行为；没有启动 VPet Main 的随机计时器，避免两个调度器竞争。常态暂停、自主隐藏与用户睡眠独立保存，瞬态指令不保存。养成仅使用现有饱食度、水分、精力、心情、亲密度；不暴露金钱、升级等额外复杂玩法。

运行时网络：本项目宿主源码无 HTTP、Steam、工坊、遥测、远程聊天或下载初始化；第三方 Localization 的网络翻译能力未调用。所有动作从安装目录读取，缓存/存档在当前用户目录。
