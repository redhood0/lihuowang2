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

    // 数值：Damage = 每次全体伤害（24，升级 +9）；SelfLoss = 自伤（22）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(24m, ValueProp.Move),
        new DynamicVar("SelfLoss", 22m)
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaqianFireSkin() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：先自伤 22，对全体敌人造成两次伤害，点燃所有敌人 3 层，弃牌堆加入 2 张灼烧。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 先自伤（若血不够会先死亡，后续不执行）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["SelfLoss"].BaseValue,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 对全体敌人造成两次伤害
        for (int i = 0; i < 2; i++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(Owner.Creature.CombatState!)
                .Execute(choiceContext);
        }

        // 3. 点燃所有敌人 3 层（受「黑手」遗物加成）
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await DianranPower.ApplyIgnite(choiceContext, enemies, 3m, Owner.Creature, this);

        // 4. 弃牌堆加入 2 张灼烧
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Discard, 2,
            Owner.Creature.Player);
    }

    // 升级：伤害 24 → 33
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(9);
    }
}
