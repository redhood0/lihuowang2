using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 怜悯（火袄真经赋予的护持）：
// 战斗期间让「灼伤」变成可以打出的牌，每回合的额度是「怜悯层数 × 2」张，每打出一张回复 4 点生命。
// 卡面说明：获得1层怜悯 / 每回合可以打出怜悯层数2倍的灼伤 / 灼伤会回复4点生命。
[RegisterPower]
public class HuoWoPower : ModPowerTemplate
{
    // 每层怜悯每回合多允许打出的灼伤数
    private const int BurnsPerStack = 1;
    // 每张打出的灼伤回复的生命
    private const int HealPerBurn = 4;

    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 需要按层数算额度，所以用可叠加的计数器（而不是 Single）
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/huowo32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/huowo84.png");

    // 本回合已经打出的灼伤数
    private int _burnsPlayedThisTurn;

    // 本回合允许打出的灼伤额度
    private int BurnsAllowedPerTurn => Amount * BurnsPerStack;

    // 战斗中改「灼伤」的关键字：去掉 [不可打出]（变成本回合可打出）、加上 [消耗]（打出后直接进消耗堆）。
    // 这是全局关键字贡献：只要这个 power 还在就生效，power 消失后自动恢复，不需要额外清理。
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (Owner.Player == null || card.Owner != Owner.Player)
            return false;
        if (card is not Burn)
            return false;

        bool changed = keywords.Remove(CardKeyword.Unplayable);
        changed |= keywords.Add(CardKeyword.Exhaust);
        return changed;
    }

    // 额度用完的灼伤不能再打出（表现为「不可打出」，UI 会照常提示原因）
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
        => card is not Burn || _burnsPlayedThisTurn < BurnsAllowedPerTurn;

    // 每个玩家回合开始时重置额度
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
            _burnsPlayedThisTurn = 0;
        return Task.CompletedTask;
    }

    // 打出灼伤：回复 2 点生命
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is not Burn || Owner.Player == null)
            return;

        _burnsPlayedThisTurn++;
        Flash();
        await CreatureCmd.Heal(Owner, HealPerBurn);
    }
}
