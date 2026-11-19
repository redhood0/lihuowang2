using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using lihuowang2.Characters;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Ancients.Options;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 火莲（先古级）：最大能量 +1；每场战斗开始时向抽牌堆塞 3 张灼烧。
//
// 获取途径：**达弗（Darv）的先古遗物**。注册见下方 RegisterDarvOption()（由 Entry.Initialize 调用）：
// 走 RitsuLib 的 RegisterAncientOption<Darv>，在达弗的初始选项里额外追加一个"火莲"。
// 稀有度是 RelicRarity.Ancient，所以不进普通遗物掉落池/商店。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_HuoLian : ModRelicTemplate
{
    // 先古遗物：引擎按稀有度取掉落与商店遗物，Ancient 天然不会出现在普通池里。
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 悬停提示：描述里点名了官方的「灼伤」，给一张卡面预览。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<Burn>()];

    // 最大能量 +1（持有期间一直生效，等价原版 energyMaster +1）。
    // ⚠ 必须判 player != Owner：引擎给每个玩家算最大能量时（PlayerCombatState.MaxEnergy）
    //   都会问一遍场上所有遗物，官方给能量的遗物（EnergyMaster 等）全都这么筛；
    //   不筛就会连队友的最大能量一起 +1（单人看不出来，只有组队才暴露）。
    //   模组里「一炁·心素」「XiuZhenPower」是同样写法，可互相参照。
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => player == Owner ? amount + 1m : amount;

    public override async Task BeforeCombatStart()
    {
        // 洗入抽牌堆：随机位置
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Draw, 3, Owner!,
            CardPilePosition.Random);
        await base.BeforeCombatStart();
    }

    // ===== 达弗（Darv）的先古遗物选项 =====

    // 由 Entry.Initialize() 调用（必须在内容注册冻结之前）。
    // Condition：玩家还没拥有火莲时才追加；想控制出现概率就在里面加 `ancient.Rng.NextBool(...)` 之类。
    // ⚠ RitsuLib 的这个 API 只是"往初始选项列表里追加"，不会替换达弗原有的遗物池（原本 2~3 个选项仍照常生成）。
    public static void RegisterDarvOption()
    {
        RitsuLibFramework.RegisterAncientOption<Darv>(Entry.ModId, ModAncientOptionRule.Single(
            CreateDarvOption,
            ancient => ancient.Owner != null
                       && ancient.Owner.GetRelic<lihuowang2Relic_HuoLian>() == null));
    }

    private static EventOption CreateDarvOption(AncientEventModel ancient)
    {
        // 官方 AncientEventModel.RelicOption 就是这么造的：先取可变实例、设 Owner（悬停/描述要读 Owner）。
        RelicModel relic = ModelDb.Relic<lihuowang2Relic_HuoLian>().ToMutable();
        relic.Owner = ancient.Owner;

        // events.json 里没有这个 key 时，EventOption.FromRelic 会回落到遗物自己的
        // Title / DynamicEventDescription，所以选项显示的就是「火莲」的名字与说明，不用另外补文案。
        string textKey = $"DARV.pages.INITIAL.options.{relic.Id.Entry}";

        return EventOption.FromRelic(relic, ancient, async () =>
        {
            await RelicCmd.Obtain(relic, ancient.Owner!);
            FinishAncient(ancient);
        }, textKey);
    }

    // 官方 AncientEventModel.RelicOption 的 onChosen 就是「RelicCmd.Obtain + Done()」，两者都是 protected：
    // 我们从外部往官方达弗里加选项，拿不到 protected 的 Done()，只能反射调用（游戏更新后若改名会在日志里报警）。
    private static readonly MethodInfo? DoneMethod = typeof(AncientEventModel)
        .GetMethod("Done", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void FinishAncient(AncientEventModel ancient)
    {
        if (DoneMethod == null)
        {
            Entry.Logger.Warn("[lihuowang2] 找不到 AncientEventModel.Done()，达弗事件可能停在本页。");
            return;
        }

        DoneMethod.Invoke(ancient, null);
    }
}
