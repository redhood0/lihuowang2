using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 炼气（重写后）：1 费，获得格挡 + 能量（基础 4 格挡 / 1 能量，升级 6 格挡 / 2 能量）。
// 额外机制：你的回合开始时，如果这张牌正躺在**消耗牌堆**里，就把它自动打出一次。
//
// 实现要点：
//  · 时点用 AfterAutoPrePlayPhaseEntered —— 引擎把"回合开始的自动打出"专门放在这个阶段
//    （PlayerTurnPhase.AutoPrePlay；官方「注能 Imbued」就是在同一钩子里 CardCmd.AutoPlay 自己的），
//    该阶段排在"能量重置"之后，所以自动打出时给的能量不会被重置冲掉；
//  · 多人：这个钩子会给每个玩家的回合开始都通知一遍，所以必须先筛 player == Owner；
//  · CardCmd.AutoPlay 不扣能量（内部 ResourceInfo.EnergySpent = 0）、也不弹预览；
//  · 打完之后这张牌按引擎规则进"结算牌堆"——本牌没有【消耗】，所以它会进弃牌堆，
//    也就是"从消耗牌堆自动打出"一次只发生一次，想再触发就得重新把它送进消耗牌堆。
//
// ⚠ 本牌自身不带【消耗】：需要外部消耗手段（黑太岁吃手牌、其它消耗效果）才能把它送进消耗牌堆。
//   如果你想要"打出后自己进消耗牌堆、于是每回合都自动再来一次"，给它加 CardKeyword.Exhaust 即可。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2LianQi : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 打出时获得的能量：基础 1 点，升级后 2 点（卡面用 {IfUpgraded:show:2|1} 显示）。
    private const int baseEnergyGain = 1;
    private const int upgradedEnergyGain = 2;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Block = 打出时获得的格挡（4，升级 +2 → 6）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(4m, ValueProp.Move)
    ];

    public lihuowang2LianQi() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：获得格挡 + 能量。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PlayerCmd.GainEnergy(IsUpgraded ? upgradedEnergyGain : baseEnergyGain, Owner);
    }

    // 回合开始：如果本牌在消耗牌堆里，就把它自动打出一次。
    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人：这个钩子会把「每个玩家的回合开始」都通知一遍，先筛出持有者本人。
        if (player != Owner)
            return;

        // 只有"此刻躺在消耗牌堆里"才触发（弃牌堆 / 抽牌堆 / 手牌都不触发）。
        if (Pile?.Type != PileType.Exhaust)
            return;

        await CardCmd.AutoPlay(choiceContext, this, null);
    }

    // 升级：格挡 4 → 6（能量 1 → 2 由 OnPlay 里的 IsUpgraded 判断）
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
    }
}
