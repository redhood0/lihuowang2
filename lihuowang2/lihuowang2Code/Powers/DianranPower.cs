using System.Collections.Generic;
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

// 点燃（挂在敌人身上的减益）：敌人回合结束时，按层数对其造成等量真实生命损失。
[RegisterPower]
public class DianranPower : ModPowerTemplate
{
    // 类型：Debuff
    public override PowerType Type => PowerType.Debuff;
    // 叠加：Counter，层数 = 每回合末灼烧的真实伤害
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dianran32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dianran84.png");

    // 敌人回合结束：对自己造成 amount 点真实伤害（等同官方 Regen 的触发时机）
    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead || base.Amount <= 0m)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, Owner, base.Amount,
            ValueProp.Unblockable, Owner, null, null);
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
