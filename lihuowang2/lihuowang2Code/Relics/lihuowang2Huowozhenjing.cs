using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 火袄真经：每当你打出一张状态牌时，将其消耗并回复 2 点生命。
// [RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Huowozhenjing : ModRelicTemplate
{
    // 稀有度
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 图片资源（先用占位图，有正式图后替换路径）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/lihuowang2Huowozhenjing.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/lihuowang2Huowozhenjing.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/lihuowang2Huowozhenjing.png");

    // 打出一张卡牌后：若是状态牌则消耗它并回 2 血
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = cardPlay.Card;
        if (card == null || card.Type != CardType.Status)
            return;
        if (Owner.Creature.Player == null)
            return;

        Flash();

        await CardCmd.Exhaust(choiceContext, card);
        await CreatureCmd.Heal(Owner.Creature, 3m);
    }
}
