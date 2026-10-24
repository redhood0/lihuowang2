using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 迷惘之力：让「迷惘」诅咒可以被主动打出，并且是**有层数的限时状态**。
// 打出那张牌时的结算（随机击晕一个敌人 + 获得 1 点能量 + 抽 1 张牌）写在卡牌本身里，
// 见 lihuowang2CurseMiWang.OnPlay。
//
// 层数 = 还能维持几个回合：
//   · 施加时默认给 DefaultStacks（1）层，见下面；
//   · 持有者每次回合结束 -1 层；层数归零时引擎自动移除本能力（卡牌随即又变回不能打出）。
//
// 本能力自己不做其它结算，它只是一个「许可」标记：lihuowang2CurseMiWang.IsPlayable 会检查
// 持有者身上有没有这件能力。
// 为什么不做成"动态关键字"：引擎的 Keywords 是实例级的缓存集合
//（CardModel.Keywords：首次访问时把 CanonicalKeywords 并进 _keywords 就缓存住了，见 _sts2_all.cs:74469），
// 关键字没法随状态变化；官方对"条件性可打出"用的就是动态 IsPlayable（Clash），模组「替身人皮」同款。
[RegisterPower]
public class MiWangZhiLiPower : ModPowerTemplate
{
    // 施加时默认给的层数（= 维持几个回合）。
    // 未来的来源（卡牌 / 遗物 / 事件）请用 PowerCmd.Apply<MiWangZhiLiPower>(..., DefaultStacks, ...) 施加。
    public const int DefaultStacks = 1;

    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // Counter = 图标上显示层数（剩余回合数），且层数只由代码手动增减。
    // 引擎不会自己 tick：TickDownDuration 只被"会自己倒计时的那些能力"（易伤/虚弱这类）显式调用，
    // 见 _sts2_all.cs:102267 / 108910 等，所以这里不会减两次。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标：复用官方「幻象」（IllusionPower）的图标 —— 与「唯一」借官方「爪牙」、
    // 「行善」借官方「肉盾」同一套做法：直接从官方模型拿路径，不手写/猜资源名
    //（IconPath = 图标行用的小图；ResolvedBigIconPath = 大图，缺失时引擎自己回落占位图）。
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel illusion = ModelDb.Power<IllusionPower>();
            return new PowerAssetProfile(
                IconPath: illusion.IconPath,
                BigIconPath: illusion.ResolvedBigIconPath);
        }
    }

    // 持有者的回合结束时 -1 层（与「天书」同一套写法：
    // Hook 会把每个阵营的回合结束都通知一遍，所以自己筛 side，多人时也不会替队友减）。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
        {
            await base.AfterSideTurnEnd(choiceContext, side, participants);
            return;
        }

        // -1 层；归零时引擎会自己把本能力移除
        //（PowerCmd.ModifyAmount → ShouldRemoveDueToAmount → Remove）。
        await PowerCmd.Decrement(this);

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }
}
