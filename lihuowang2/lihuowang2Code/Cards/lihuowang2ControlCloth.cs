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
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 拘束服：本场战斗 -3 力量，换来多层护甲。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ControlCloth : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<PlatingPower>()];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Plating = 多层护甲层数（6，升级 +2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Plating", 6m)
    ];

    public lihuowang2ControlCloth() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 本场战斗失去 3 点力量（负值力量；战斗结束后自动清除）
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, -3m, Owner.Creature, this);

        // 2. 获得多层护甲
        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature,
            DynamicVars["Plating"].BaseValue, Owner.Creature, this);
    }

    // 升级：多层护甲 6 → 8
    protected override void OnUpgrade()
    {
        DynamicVars["Plating"].UpgradeValueBy(2);
    }
}
