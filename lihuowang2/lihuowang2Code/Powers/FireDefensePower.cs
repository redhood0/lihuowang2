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

// 神火护体（Buff）：本回合内受到敌方攻击时，点燃所有敌人 1 层；下个自己回合开始时移除。
[RegisterPower]
public class FireDefensePower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 单层、不叠加
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dianran32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dianran84.png");

    // 被敌人攻击时：给所有敌人 1 层点燃
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || dealer == null || dealer == Owner || Owner.IsDead)
            return;

        Flash();

        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies;
        await DianranPower.ApplyIgnite(choiceContext, enemies, 1m, Owner, cardSource);
    }

    // 下个自己回合开始时失效
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
