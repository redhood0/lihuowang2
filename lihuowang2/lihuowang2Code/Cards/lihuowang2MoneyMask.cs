using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
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

// 铜钱面具：获得多层护甲并直接捞一笔金币。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MoneyMask : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 悬停提示：描述里出现的「多层护甲」和「铜钱」
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [
            HoverTipFactory.FromPower<PlatingPower>(),
            HoverTipFactory.FromPower<MoneyPower>()
        ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Plating = 多层护甲层数（3，升级 +2 → 5）；Gold = 获得的金币（10，升级 +5 → 15）。
    // 铜钱固定 1 枚（升级不变，且有 5 层上限），所以不占变量、直接写在文案里。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Plating", 3m),
        new DynamicVar("Gold", 10m)
    ];

    public lihuowang2MoneyMask() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature,
            DynamicVars["Plating"].BaseValue, Owner.Creature, this);
        await PlayerCmd.GainGold(DynamicVars["Gold"].BaseValue, player);

        // 3. 获得 1 枚铜钱（走统一入口：已满 5 层就不再叠加）
        await MoneyPower.ApplyMoney(choiceContext, Owner.Creature, Owner.Creature, this);
    }

    // 升级：多层护甲 3 → 5；金币 10 → 15（铜钱不变）
    protected override void OnUpgrade()
    {
        DynamicVars["Plating"].UpgradeValueBy(2);
        DynamicVars["Gold"].UpgradeValueBy(5);
    }
}
