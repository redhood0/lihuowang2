using System.Collections.Generic;
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

// 煞气：你的普通攻击伤害 +层数；
// 当煞气 ≥ 5 时，每个自己回合开始时给所有敌人 1 层易伤，并且你无法再获得格挡。
[RegisterPower]
public class ShaQiPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加：Counter，层数 = 攻击伤害加成
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/shaqi32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/shaqi84.png");

    // 造成的普通（受力量影响）攻击伤害 +层数
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner)
            return 0m;
        if (!props.IsPoweredAttack())
            return 0m;
        return base.Amount;
    }

    // 煞气 ≥ 5 时无法获得格挡。
    // 走「乘算」那一档：所有格挡来源（卡牌/能力/遗物）都会经过 Hook.ModifyBlock，
    // 返回 0 就等于拿不到格挡；顺带卡面预览也会显示 0 格挡，和实际一致。
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && base.Amount >= 5)
            return 0m;
        return 1m;
    }

    // 自己回合开始：煞气 ≥ 5 时给所有敌人 1 层易伤
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
            return;
        if (base.Amount < 5m)
            return;

        Flash();

        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemies, 1m, Owner, null);
    }
}
