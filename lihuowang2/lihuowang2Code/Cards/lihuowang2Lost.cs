using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
// [RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Lost : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 抽牌基准（1，升级 +1 → 2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 1m)
    ];

    public lihuowang2Lost() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 抽 Magic 张牌；若拥有黑太岁：移除之并按层数 × Magic 再抽
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal magic = DynamicVars["Magic"].BaseValue;
        await CardPileCmd.Draw(choiceContext, magic, Owner.Creature.Player!);

        Lihuowang2HeitaisuiPower? heitaisui = Owner.Creature.GetPower<Lihuowang2HeitaisuiPower>();
        if (heitaisui == null)
            return;

        decimal extraDraw = heitaisui.Amount * magic;
        await PowerCmd.Remove(heitaisui);
        if (extraDraw > 0)
            await CardPileCmd.Draw(choiceContext, extraDraw, Owner.Creature.Player!);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
