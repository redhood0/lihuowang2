using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 神火护体（Buff）：本回合内受到敌方攻击时，点燃所有敌人 N 层（N = 打出「神火护体」时写入的层数，
// 基础 2 / 升级 3）；下个自己回合开始时移除。
[RegisterPower]
public class FireDefensePower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 层数可见（图标上会显示数字）且需要手动赋值，所以用 Counter。
    // 不用 Single：引擎里 Single 的层数「不显示」（RefreshAmount 只对 Counter 显示数字），
    // 而这里玩家需要直接看到会点燃几层；卡牌侧每次都先移除旧实例再按当前数值施加，所以不会累加。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dianran32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dianran84.png");

    // 被敌人攻击时：给所有敌人点燃若干层（层数 = 本能力当前的层数）
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || dealer == null || dealer == Owner || Owner.IsDead)
            return;

        Flash();

        // 层数由卡牌写入；兜底至少 1 层（正常不会出现 0，实体能力不会被以 0 层施加）。
        decimal layers = Math.Max(Amount, 1);

        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies;
        await DianranPower.ApplyIgnite(choiceContext, enemies, layers, Owner, cardSource);
    }

    // 下个自己回合开始时失效
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
