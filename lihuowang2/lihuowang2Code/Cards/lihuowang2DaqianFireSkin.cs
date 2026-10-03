using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using lihuowang2.Tags;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianFireSkin : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 2;
    // 卡牌类型（攻击）
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AllEnemies：全体敌人）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 群攻段数：固定 2 段（升级只涨每段伤害，不涨段数），所以写成常量。
    private const int aoeHitCount = 2;

    // 大千录 tag
    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu
    ];

    // 悬停提示：点燃效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<DianranPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianFireSkin.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：
    // Damage   = 每次全体伤害（12，升级 +4 → 16）；
    // Ignite   = 点燃层数（4，升级 +2 → 6）；
    // SelfLoss = 自伤（16，升级不变化）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(12m, ValueProp.Move),
        new DynamicVar("Ignite", 4m),
        new DynamicVar("SelfLoss", 16m)
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaqianFireSkin() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：先自伤 16，对全体敌人造成两次伤害，点燃所有敌人（Ignite 层），弃牌堆加入 2 张灼烧。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 先自伤（若血不够会先死亡，后续不执行）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["SelfLoss"].BaseValue,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 对全体敌人造成两次伤害。
        //    与官方群攻多段牌「匕首雨」（DaggerSpray）同款写法：一次攻击命令跑完 2 段，
        //    而不是循环调用两次 DamageCmd.Attack（那样会播两遍完整攻击动作）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(aoeHitCount)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .WithHitFx("vfx/vfx_fire_burst")   // 命中特效：火焰爆燃（烈火焚身）
            .Execute(choiceContext);

        // 3. 点燃所有敌人（层数取 Ignite；受「黑手」遗物加成）
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await DianranPower.ApplyIgnite(choiceContext, enemies,
            DynamicVars["Ignite"].BaseValue, Owner.Creature, this);

        // 4. 弃牌堆加入 2 张灼烧
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Discard, 2,
            Owner.Creature.Player);
    }

    // 升级：每次伤害 12 → 16；点燃 4 → 6（自伤与灼烧张数不变）
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["Ignite"].UpgradeValueBy(2);
    }
}
