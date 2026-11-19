using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Relics;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 点燃（减益）：被点燃的生物在**自身回合开始时**按层数递减结算，然后层数归零。
// 结算方式：N 层 → 先失去 N 点生命、层数 -1；再失去 N-1 点、层数 -1 …… 直到 0 层。
// 合计 = N + (N-1) + … + 1 = N*(N+1)/2 点（4 层 = 4+3+2+1 = 10 点）。
// ⚠ 上述"举例说明"只写在这里给开发者看，卡面文案不展开细节（见 powers.json 的描述）。
[RegisterPower]
public class DianranPower : ModPowerTemplate
{
    // 类型：Debuff
    public override PowerType Type => PowerType.Debuff;
    // 叠加：Counter，层数 = 这次点燃的强度（回合开始时按它递减结算）
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dianran32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dianran84.png");

    // 自身回合开始：一次性结算"层数递减"的总和，然后清空层数。
    //
    // ⚠ 必须用 AfterSideTurnStart（官方 PoisonPower 同款），**不要**用 BeforeSideTurnStart：
    //   在"回合真正开始之前"把单位打死时，引擎还没走完死亡结算，敌人回合的行动队列会卡死
    //   （实测反馈：点燃烧死一只敌人、同侧另一只怪还挂着攻击意图 → 战斗卡住不动）。
    //   官方中毒也是在这个钩子里结算，并且死亡后额外等一小段死亡动画。
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.IsDead)
            return;

        int layers = base.Amount;
        if (layers <= 0)
            return;

        Flash();

        // 4 层 = 4+3+2+1；直接算总和一次结算，不逐层循环：
        // 层数可能叠得很高（黑手、多张火牌、群体点燃），逐层会刷出几十上百次伤害事件与飘字。
        // 伤害标记与官方中毒一致：Unblockable（不吃格挡）+ Unpowered（也不吃力量/易伤等增伤修正）。
        decimal total = (decimal)layers * (layers + 1) / 2m;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, total,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        if (Owner.IsAlive)
        {
            // 层数归零：移除这个能力（图标上的数字随之消失）。
            await PowerCmd.Remove(this);
        }
        else
        {
            // 被烧死：给死亡结算/动画留时间再返回，否则同侧其它敌人的行动会卡在那一帧（官方 Poison 同款）。
            await Cmd.CustomScaledWait(0.1f, 0.25f);
        }
    }

    // ---------- 统一施加点燃的入口 ----------
    // 所有火系卡/效果都走这里：若施加者（玩家）拥有遗物「黑手」，则层数 +1。
    public static async Task ApplyIgnite(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        decimal finalAmount = amount;
        if (applier != null && applier.Player != null && applier.Player.GetRelic<lihuowang2Blackhand>() != null)
            finalAmount += 1m;

        await PowerCmd.Apply<DianranPower>(choiceContext, targets, finalAmount, applier, cardSource);
    }

    public static async Task ApplyIgnite(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        decimal finalAmount = amount;
        if (applier != null && applier.Player != null && applier.Player.GetRelic<lihuowang2Blackhand>() != null)
            finalAmount += 1m;

        await PowerCmd.Apply<DianranPower>(choiceContext, target, finalAmount, applier, cardSource);
    }
}
