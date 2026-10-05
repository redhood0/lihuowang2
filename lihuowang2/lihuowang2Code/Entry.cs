using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using lihuowang2.Characters;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace lihuowang2;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // ModId 需要和 lihuowang2.json 里的 id 保持一致。
    // res://lihuowang2/... 里的 lihuowang2 是 PCK 资源目录，不是 C# namespace。
    public const string ModId = "lihuowang2";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 以下示例默认已经在 Entry.Initialize() 中调用了
        // RitsuLibFramework.EnsureGodotScriptsRegistered(...) 和
        // ModTypeDiscoveryHub.RegisterModAssembly(...)，否则自动注册不会生效。
        //
        // Godot C# 脚本注册只负责让 pck 中的脚本类型能被 Godot 找到。
        // 这一步和 RitsuLib 的内容自动注册不是同一件事，两个都需要保留。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 自动注册扫描会读取当前程序集里的 RegisterCard/RegisterRelic 等 attribute。
        // 新增内容类后，只要 attribute 写对，通常不需要在入口里手动逐个注册。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 借用《杀戮尖塔2》铁甲战士的非初始职业专属遗物（原版模型，稀有度保持原样）。
        //
        // ⚠ 这里必须用引擎自己的 ModHelper.AddModelToPool，**不要**用 RitsuLib 的
        //   ModContentRegistry.RegisterRelic —— 两者结果不一样：
        //     · RegisterRelic：会把模型"收编"为模组所有，并给它分配新条目
        //       <模组ID>_<类别>_<类名>（日志里能看到 id=LIHUOWANG2_RELIC_RED_SKULL）。
        //       条目一变，图标和文案就按新条目去查，而模组并没有这些资源，于是：
        //         图标 → 去 relic_atlas / relic_outline_atlas 找 'lihuowang2_relic_red_skull'
        //                → Missing sprite，游戏里显示报错图；
        //         文案 → 去 relics 表找 'LIHUOWANG2_RELIC_RED_SKULL.title/.description'
        //                → not found，标题/说明为空。
        //     · AddModelToPool：只是把"类型"追加进卡池，模型仍保留原版身份（RED_SKULL），
        //       图标与中英文文案全部直接沿用本体资源，不需要我们补图、补文案。
        //
        // 初始遗物「燃烧之血」是角色起始遗物，不在此列。
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(RedSkull));          // 红头骨（普通）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(PaperPhrog));        // 纸蛙（罕见）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(SelfFormingClay));   // 自成型黏土（罕见）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(CharonsAshes));      // 卡戎之灰（稀有）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(DemonTongue));       // 恶魔之舌（稀有）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(RuinedHelmet));      // 损毁头盔（稀有）
        ModHelper.AddModelToPool(typeof(lihuowang2RelicPool), typeof(Brimstone));         // 硫磺（商店）

        // 注：曾经给「怜悯」的灼烧做过"可指定友方目标并给目标回血"（6 个 vanilla 补丁），已按需求回退，
        // 现在灼烧回到官方行为（不可指定目标，打出后固定回自己血）；相关经验记在 AGENTS.md 的"补丁备忘"里。

        // 模组设置（设置中心里的开关 + settings.json 持久化）。
        // 必须在 ModTypeDiscoveryHub 之后：设置页要挂在已注册的内容包上。
        Lihuowang2ModSettings.Initialize();

        Logger.Info("lihuowang2 initialized.");
    }
}
