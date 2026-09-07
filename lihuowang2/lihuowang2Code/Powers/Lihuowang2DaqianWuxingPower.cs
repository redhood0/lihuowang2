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

// 大千录·置闰五行的持续效果。
// Amount = 剩余回合倒计时（图标角标直接显示）。
[RegisterPower]
public class Lihuowang2DaqianWuxingPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加类型：Counter（Amount 即倒计时数字）
    public override PowerStackType StackType => PowerStackType.Counter;

    // 自定义图标路径。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/wuxing32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/wuxing84.png"
    );

    // 受到伤害时，回复等量生命
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || Owner.IsDead)
            return;

        Flash();
        await CreatureCmd.Heal(Owner, amount);
    }

    // 自己回合开始时：倒计时 -1；归零时对自己造成巨额真实伤害触发死亡（可被免死/复活类效果规避）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 只对自己的回合生效
        if (player.Creature != Owner)
            return;

        if (base.Amount > 1m)
        {
            // 还有 1 个以上的回合：倒计时 -1
            await PowerCmd.ModifyAmount(choiceContext, this, -1m, Owner, null);
            return;
        }

        // 倒计时已到：先移除效果避免重复触发，再对本体造成巨额真实伤害
        await PowerCmd.Remove(this);
        await CreatureCmd.Damage(choiceContext, Owner, 999999m,
            ValueProp.Unblockable, Owner, null, null);
    }
}
