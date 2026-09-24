using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 触手·缚：需要黑太岁才能打出；勒颈（Strangle，官方机制）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2TentacleBind : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里的「勒颈」，以及打出条件里的「黑太岁」。
    // 勒颈是官方能力（StranglePower：对方每打出一张牌就掉当前层数的血，回合结束时移除），
    // 文案取官方 powers 表，不需要自己写。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [
            HoverTipFactory.FromPower<StranglePower>(),
            HoverTipFactory.FromPower<Lihuowang2HeitaisuiPower>()
        ];

    // Damage = 伤害（3，升级 +3 → 6）；Heal = 被消耗时回复的生命（3，升级 +1 → 4）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Heal", 3m)
    ];

    public lihuowang2TentacleBind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 需要黑太岁才能打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.GetPower<Lihuowang2HeitaisuiPower>() != null;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 勒颈 2 层
        await PowerCmd.Apply<StranglePower>(choiceContext, cardPlay.Target!, 2m, Owner.Creature, this);
    }

    // 被消耗时回复生命（任何来源的消耗都算）。
    // 不要在别处（例如黑太岁能力里）再判一次，否则会重复回血。
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card != this)
            return;

        await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
    }

    // 升级：伤害 3 → 6；被消耗时回血 3 → 4
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Heal"].UpgradeValueBy(1);
    }
}
