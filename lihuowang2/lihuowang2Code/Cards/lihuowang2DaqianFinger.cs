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
public class lihuowang2DaqianFinger : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（AnyEnemy表示任意敌人）
    private const TargetType targetType = TargetType.AnyEnemy;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 6m;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianFinger.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值（对目标造成的无视格挡伤害）。升级后 +4（22 → 26）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(22, ValueProp.Unblockable)
    ];

    // 默认关键字：保留、消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain, CardKeyword.Exhaust
    ];

    public lihuowang2DaqianFinger() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑：对目标造成无视格挡的伤害，同时自己失去 6 点生命
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 对目标造成 22 点无视格挡的伤害（Unblockable，不受护甲影响）
        await CreatureCmd.Damage(choiceContext, cardPlay.Target!, DynamicVars.Damage.BaseValue,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 自己失去 6 点生命（Unblockable）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    // 升级后的效果逻辑：无视格挡伤害 22 → 26
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}
