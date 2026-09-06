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
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2PlayFire : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型（攻击）
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AllEnemies：全体敌人）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 悬停提示：点燃效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<DianranPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2PlayFire.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Damage = 全体伤害（11，升级 +4）；Ignite = 点燃层数（1，升级 +1）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(11m, ValueProp.Move),
        new DynamicVar("Ignite", 1m)
    ];

    public lihuowang2PlayFire() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：对全体敌人造成伤害，弃牌堆加入 2 张灼烧，并点燃所有敌人。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 对全体敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .Execute(choiceContext);

        // 2. 弃牌堆加入 2 张灼烧
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Discard, 2,
            Owner.Creature.Player);

        // 3. 点燃所有敌人（受「黑手」遗物加成）
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await DianranPower.ApplyIgnite(choiceContext, enemies,
            DynamicVars["Ignite"].BaseValue, Owner.Creature, this);
    }

    // 升级：伤害 11 → 15；点燃 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["Ignite"].UpgradeValueBy(1);
    }
}
