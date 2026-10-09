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
// 达到阈值后还有两个惩罚：无法从卡牌中获得格挡 / 每回合开始给所有敌人易伤。
// 两个阈值是**分开**的常量（见下），各改各的。
[RegisterPower]
public class ShaQiPower : ModPowerTemplate
{
    // ===== 可调阈值 =====
    // 煞气达到这个层数后：无法从卡牌获得格挡。
    public const int BlockPreventStacks = 3;
    // 煞气达到这个层数后：每回合开始时对所有敌人施加 1 层易伤。
    public const int VulnerableStacks = 5;
    // ⚠ 改这两个数字时，记得同步 powers 本地化文案里的数字
    //   （LIHUOWANG2_POWER_SHA_QI_POWER.description / .smartDescription）。

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

    // 煞气 ≥ BlockPreventStacks 时无法从「卡牌」获得格挡（能力/遗物给的格挡照常生效）。
    // 判定依据是 cardSource：引擎文档写明「Card that will be adding the block.
    // Null if the block is coming from something other than a card (like a Relic)」，
    // 所以 cardSource != null 就代表这一笔格挡来自卡牌。
    // 走「乘算」那一档：格挡结算统一经过 Hook.ModifyBlock，返回 0 即拿不到；
    // 手牌预览时 cardSource 也有值（cardPlay 才是 null），所以卡面会显示 0 格挡，和实际一致。
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner && base.Amount >= BlockPreventStacks && cardSource != null)
            return 0m;
        return 1m;
    }

    // 自己回合开始：煞气 ≥ VulnerableStacks 时给所有敌人 1 层易伤
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
            return;
        if (base.Amount < VulnerableStacks)
            return;

        Flash();

        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemies, 1m, Owner, null);
    }
}
