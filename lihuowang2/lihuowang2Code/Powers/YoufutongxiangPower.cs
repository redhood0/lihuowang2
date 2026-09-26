using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 有福同享：自己因自身效果失去生命（自伤/真实掉血）时，
// 对所有敌人各造成「该数值 × 层数」的真实伤害（无视格挡）。
[RegisterPower]
public class YoufutongxiangPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加：Counter，层数为伤害倍率
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/painshare32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/pianshare84.png");

    // 受伤即将发生时：若伤害来源是自己（自伤/真实掉血），则让所有敌人承受等量 × 层数的真实伤害
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m)
            return;
        // 只响应自身造成的真实生命损失（与大千录系自伤一致）
        if (dealer != Owner || !props.HasFlag(ValueProp.Unblockable))
            return;

        Flash();

        decimal reflect = amount * base.Amount;

        // 关键：先做快照再逐个结算。CombatState.Enemies 直接返回战斗中的实时列表(_enemies)，
        // 边遍历边造成伤害时，一旦有敌人被打死（以及死亡触发的移出/召唤/尸爆等），
        // 列表版本号就会变化，下一次 MoveNext 会抛 InvalidOperationException；
        // 这个异常发生在异步伤害钩子里，游戏不会弹错，而是表现为战斗卡死。
        // （同 lihuowang2BreakSpace.cs 里的做法："用快照副本遍历，防止敌人中途被移除导致枚举异常"）
        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies.ToArray();
        foreach (Creature enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            await CreatureCmd.Damage(choiceContext, enemy, reflect,
                ValueProp.Unblockable, Owner, null, null);
        }
    }
}
