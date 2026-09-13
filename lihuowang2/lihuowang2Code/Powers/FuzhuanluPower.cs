using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 符篆录：每当你打出一张符篆牌（当前为「神行符」等）时抽 Amount 张牌；
// 打出次数累计超过 3 次后，额外将 1 张「大千录·剜眼」加入手牌。

public class FuzhuanluPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 单层
    public override PowerStackType StackType => PowerStackType.Single;

    // 本场战斗打出的符篆计数
    private int _fuCount;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/fuzhuanlupower32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/fuzhuanlupower84.png");

    // 打出一张卡牌后：若为符篆牌则抽牌（并累计）
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = cardPlay.Card;
        if (card == null)
            return;

        // 符篆牌判定（后续新增符篆卡时在此追加类型）
        if (card is not lihuowang2ShenXingFu)
            return;

        Flash();

        // 抽 Amount 张牌
        await CardPileCmd.Draw(choiceContext, base.Amount, Owner.Player!);

        // 超过 3 张后，塞一张剜眼进手牌
        _fuCount++;
        if (_fuCount > 3)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2DaqianEye>(
                Owner, PileType.Hand, 1, Owner.Player);
        }
    }
}
