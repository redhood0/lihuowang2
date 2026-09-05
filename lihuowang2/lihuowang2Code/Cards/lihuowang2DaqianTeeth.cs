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
public class lihuowang2DaqianTeeth : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（RandomEnemy：随机敌人，无需玩家选择目标）
    private const TargetType targetType = TargetType.RandomEnemy;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 3m;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianTeeth.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：
    // Damage = 每次随机命中的伤害（4）；Repeat = 随机攻击的次数（升级后 +1 → 5）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(4, ValueProp.Move),
        new RepeatVar(4)
    ];

    // 默认关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaqianTeeth() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑：随机对敌人造成伤害多次，同时自己失去 3 点生命
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 随机攻击敌人：每次命中随机选一个敌人，共攻击 {Repeat} 次，每次造成 {Damage} 点伤害
        int hits = (int)DynamicVars.Repeat.BaseValue;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .WithHitCount(hits)
            .TargetingRandomOpponents(Owner.Creature.CombatState!)
            .Execute(choiceContext);

        // 2. 自己失去 3 点生命（Unblockable：无视格挡的真实 HP 损失）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    // 升级后的效果逻辑：随机攻击次数 4 → 5
    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}
