using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 触手·愈：需要黑太岁才能打出；格挡两次。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2TentacleMend : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：打出条件里的「黑太岁」（Lihuowang2HeitaisuiPower）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<Lihuowang2HeitaisuiPower>()];

    // Block = 每次格挡（3，升级 +1 → 4）；Heal = 被消耗时回复的生命（3，升级 +1 → 4）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(3m, ValueProp.Move),
        new DynamicVar("Heal", 3m)
    ];

    public lihuowang2TentacleMend() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
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

    // 获得两次格挡
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
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

    // 升级：格挡 3 → 4；被消耗时回血 3 → 4
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1);
        DynamicVars["Heal"].UpgradeValueBy(1);
    }
}
