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

// 岁岁公主（李岁·公主形态）的能力：触手牌造成的伤害与获得的格挡 +层数。
// 与「李岁·狗形态」（Lihuowang2LisuiPower）的区别：这里没有每回合 AOE，只有触手加成。
//
// 加成值就直接是**能力层数（Amount）**：
//   · 图标上会显示这个数字，也就是「+X」里的 X；
//   · 打出 1 张 X=2 的岁岁公主 → 层数 2（图标 2，说明 +2）；
//   · 再打一张（第二张岁岁公主）→ 层数 4 → 加成变成 +4（多张会相加，Counter 施加即累加）；
//   · 升级过的那张（X=4）打出 → 在原层数上 +4。
// 说明文本里用 {Amount} 引用它：引擎在所有悬停路径都会注入 Amount
// （包括"dumb"路径，见 PowerModel.AddDumbVariablesToDescription），
// 所以不需要像自定义变量那样补 Description 覆写。
[RegisterPower]
public class Lihuowang2LisuiGongzhuPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    // Counter：图标显示层数，且重复施加会累加（层数 = 加成值）
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先复用李岁（lisui32/84）的图，有专属图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/suisuigongzhu.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/suisuigongzhu.png");

    // 触手攻击伤害加成（= 当前层数）
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || !Lihuowang2LisuiPower.IsTentacle(cardSource))
            return 0m;
        return Amount;
    }

    // 触手格挡加成（= 当前层数）
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || !Lihuowang2LisuiPower.IsTentacle(cardSource))
            return 0m;
        return Amount;
    }

    // 触手牌「施加能力」时，层数也吃这份加成（隐藏机制，说明文案里不写）。
    //
    // 引擎的「施加方加成」钩子（官方 SneckoSkull 遗物同款，遍历战斗内的所有能力/遗物）：
    //   · power       = 即将被施加的能力
    //   · giver       = 施加者（本能力持有者 = 玩家自己）
    //   · cardSource  = 来源卡（用来认触手牌）
    // 触手系列里目前只有「触手·缚」会给敌人挂官方「勒颈」（StranglePower），
    // 所以表现就是：打出触手·缚 → 勒颈层数 = 2 + X（与伤害/格挡用的是同一个 X）。
    // 好处：卡牌侧一行都不用改，也不会因为"漏了某张牌"而失效 —— 以后新增触手牌给别的能力加层数，
    // 只要把下面的判定放宽（去掉 `power is not StranglePower`）就会自动生效。
    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount,
        Creature? target, CardModel? cardSource)
    {
        if (giver != Owner || power is not StranglePower || !Lihuowang2LisuiPower.IsTentacle(cardSource))
            return 0m;
        return Amount;
    }
}
