using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 魍魉之躯：每有 2 张诅咒牌进入自己的手牌，获得 amount 点力量。
[RegisterPower]
public class WangLiangBodyPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加：Counter，层数 = 每 2 张诅咒给予的力量
    public override PowerStackType StackType => PowerStackType.Counter;

    // 已「进入手牌」的诅咒计数（满 2 张结算一次并清零）
    private int _curseEnteredHand;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/wangliang32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/wangliang84.png");

    // 用「换堆」钩子而不是「抽牌」钩子：抽到只是诅咒进入手牌的其中一条路径，
    // 效果直接把诅咒塞进手牌（疑虑、丹炉炸炉的诅咒、大千录系列等）不会触发抽牌钩子，
    // 那样统计就会漏。
    // 判定条件：这张牌是诅咒 + 此刻在手牌里 + 之前不在手牌。
    // 后两条一起用可以排除两类干扰：手牌内部重排（旧堆也是手牌），
    // 以及卡牌离开手牌/被消耗时那次换堆通知（那时 card.Pile 仍是旧堆或为空）。
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Type != CardType.Curse)
            return;
        if (card.Pile?.Type != PileType.Hand)
            return;
        if (oldPileType == PileType.Hand)
            return;
        // 只统计自己的能力主人（多人/宠物持有手牌的情况）
        if (card.Owner.Creature != Owner)
            return;

        _curseEnteredHand++;
        if (_curseEnteredHand < 2)
            return;

        _curseEnteredHand = 0;
        Flash();
        // 该钩子没有 PlayerChoiceContext，施加力量本身也不会产生玩家选择，
        // 所以用 ThrowingPlayerChoiceContext（模组里其他无上下文钩子也是这么写的）。
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner, base.Amount, Owner, null);
    }
}
