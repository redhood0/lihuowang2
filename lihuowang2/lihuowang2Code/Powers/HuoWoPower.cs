using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 火袄护持：每当你打出一张状态牌时，将其消耗并回复 4 点生命。
[RegisterPower]
public class HuoWoPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 单层
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/huowo32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/huowo84.png");

    // 打出一张卡牌后：若是状态牌则消耗它并回 4 血
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = cardPlay.Card;
        if (card == null || card.Type != CardType.Status)
            return;
        if (Owner.Player == null)
            return; // 只对玩家自己生效

        Flash();

        // 尝试把打出的状态牌消耗掉
        await CardCmd.Exhaust(choiceContext, card);

        // 回复 4 点生命
        await CreatureCmd.Heal(Owner, 4m);
    }
}
