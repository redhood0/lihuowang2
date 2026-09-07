using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BearPain : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 格挡牌（UI 会据此识别）
    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Block = 格挡（12，升级 +3 → 15）；SelfLoss = 自伤（2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("SelfLoss", 2m)
    ];

    public lihuowang2BearPain() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 获得格挡，然后自己失去 2 点生命（真实掉血）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["SelfLoss"].BaseValue,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
    }
}
