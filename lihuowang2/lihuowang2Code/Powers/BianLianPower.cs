using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 变脸：持有期间不会受到其它怪物的伤害（伪装成一种怪物）。
//
// Amount = 伪装的剩余回合数，图标角标直接显示这个倒计时：
//   · 每个「自己的回合开始」-1，归零即移除能力 → 变回默认形象；
//   · 自己**主动**打出一张攻击牌时立刻结束（自动打出不算，见 CardPlay.IsAutoPlay）。
//   · 重复打出变脸是 Counter 的常规语义：叠加层数 = 延长伪装，同时刷新形象。
//
// 形象由卡牌（lihuowang2BianLian）挑一种怪物后调用 Lihuowang2VisualUtil 挂上去；
// 结束（倒计时归零 / 打出攻击牌 / 被「迷失」移除）时由 AfterRemoved 统一还原成默认形象。
[RegisterPower]
public class BianLianPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // Counter：Amount 即倒计时数字，会显示在图标角标上
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标：借用官方能力「爪牙」（MegaCrit.Sts2.Core.Models.Powers.MinionPower）的图标。
    // 与 UniquePower 同款做法：直接问官方模型要路径，不手写/猜资源名。
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel minion = ModelDb.Power<MinionPower>();
            return new PowerAssetProfile(
                IconPath: minion.IconPath,
                BigIconPath: minion.ResolvedBigIconPath);
        }
    }

    // 不受其它怪物的伤害：把伤害算成 0。
    // 写在"伤害计算"这一档（ModifyDamageMultiplicative）而不是扣血那一档 ——
    // 这样整笔伤害就是 0，格挡不会被消耗，也不会出现"先扣格挡再减免"的旧语义（同 CrazyPower 的取舍）。
    //
    // ⚠ 判定必须完全确定性（多人两端算出同一个值，否则会状态分歧）：
    //   只看「目标是自己」+「施加者是怪物」，两项都是内容本身的信息。
    //   绝不要用 LocalContext.IsMe(target.Player) / target.IsPlayer 这类"按机器"的条件 ——
    //   CrazyPower 里记过那条坑（只有受击者本机返回 true → 两端数值不同 → 校验和分歧）。
    // 自伤（dealer == Owner）以及非怪物造成的伤害照常结算。
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && dealer != null && dealer != Owner && dealer.IsMonster)
            return 0m;

        return 1m;
    }

    // 自己的回合开始：倒计时 -1；归零则移除能力（AfterRemoved 负责变回默认形象）。
    // 与「大千录·置闰五行」的倒计时同一写法。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人：这个钩子会把「每个玩家的回合开始」都通知给场上所有能力，
        // 所以必须先筛出"这次轮到的就是能力持有者"，否则会替队友倒数。
        if (player.Creature != Owner)
            return;

        if (Amount > 1m)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -1m, Owner, null);
            return;
        }

        await PowerCmd.Remove(this);
    }

    // 主动打出攻击牌 → 伪装立刻结束。
    // "主动"用 CardPlay.IsAutoPlay 区分：遗物/能力自动打出的攻击牌不算（引擎注释：手动从手牌打出时为 false）。
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card == null || cardPlay.Card.Type != CardType.Attack)
            return;

        // 多人：引擎会把"所有玩家打出的牌"都通知给场上所有能力，先筛掉队友打的牌。
        if (cardPlay.Player.Creature != Owner)
            return;

        if (cardPlay.IsAutoPlay)
            return;

        await PowerCmd.Remove(this);
    }

    // 能力被移除时（倒计时归零 / 打出攻击牌 / 被「迷失」移除）把战斗形象还原成**变脸前那一具**：
    //   · 变脸前是默认图 → 回到默认图；
    //   · 变脸前正处在黑太岁形态 → 回到黑太岁形态（而不是一律回默认）。
    // 快照由卡牌在换形象前拍下（Lihuowang2VisualUtil.CaptureBodyForFaceForm）。
    public override Task AfterRemoved(Creature oldOwner)
    {
        Lihuowang2VisualUtil.RestoreBodyAfterFaceForm(oldOwner);
        return base.AfterRemoved(oldOwner);
    }
}
