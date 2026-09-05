using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianEye : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 8m;

    // 这是格挡牌（UI 会据此识别）
    public override bool GainsBlock => true;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianEye.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：
    // Block = 获得的格挡（14）；Energy = 获得的能量（升级后 +1 → 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(14m, ValueProp.Move),
        new EnergyVar(1)
    ];

    // 默认关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaqianEye() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑：失去生命，获得格挡，获得能量
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 自己失去 8 点生命（Unblockable：无视格挡的真实 HP 损失）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 3. 获得能量（玩家 creature 必有对应的 Player）
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner.Creature.Player!);
    }

    // 升级后的效果逻辑：获得的能量 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1);
    }
}
