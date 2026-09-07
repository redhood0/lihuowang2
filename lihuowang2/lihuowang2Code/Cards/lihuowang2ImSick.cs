using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ImSick : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2ImSick() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：进入疯癫，并把疯狂类诅咒洗入抽牌堆
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CrazyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 洗入：疯狂（自建无色）+ 疑虑 + 悔恨 + 扭曲
        await CardPileCmd.AddToCombatAndPreview<Doubt>(Owner.Creature, PileType.Draw, 1, Owner.Creature.Player);
        await CardPileCmd.AddToCombatAndPreview<Regret>(Owner.Creature, PileType.Draw, 1, Owner.Creature.Player);
        await CardPileCmd.AddToCombatAndPreview<Writhe>(Owner.Creature, PileType.Draw, 1, Owner.Creature.Player);
        await CardPileCmd.AddToCombatAndPreview<lihuowang2Madness>(Owner.Creature, PileType.Draw, 1, Owner.Creature.Player);
    }

    // 升级：获得固有
    protected override void OnUpgrade()
    {
        CardCmd.ApplyKeyword(this, CardKeyword.Innate);
    }
}
