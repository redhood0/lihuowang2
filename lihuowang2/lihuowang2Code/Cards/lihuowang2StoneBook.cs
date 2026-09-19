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
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 天书护体：本场战斗 -2 敏捷，换取「每回合结束 +6 格挡」的天书护体。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2StoneBook : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的「天书」（Lihuowang2StoneBookPower）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<Lihuowang2StoneBookPower>()];

    // Magic = 失去的敏捷（2，升级不变）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 2m)
    ];

    public lihuowang2StoneBook() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 本场战斗失去 2 点敏捷（负值敏捷；战斗结束后自动清除）
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature,
            -DynamicVars["Magic"].BaseValue, Owner.Creature, this);

        // 2. 获得天书
        await PowerCmd.Apply<Lihuowang2StoneBookPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    // 升级：费用 1 → 0，且固有
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Innate);
    }
}
