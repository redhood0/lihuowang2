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

    // 受到伤害后：把「实际失去的生命」补回来。
    //
    // 必须用后置钩子（AfterDamageReceived），不能在前置钩子里回血，这里踩过两个坑：
    //  1. 前置回血会被最大生命上限截断：上限 80、当前 55、自伤 40 时，先回 40
    //     （55+40=95 → 截成 80）再扣 40 → 只剩 40 血；后置回血才是
    //     55 → 15 → 回 40 → 55，净效果「不掉血」。
    //  2. 致死伤害在引擎里根本不会调用本钩子：CreatureCmd.Damage 对
    //     WasTargetKilled && IsDead 的目标走 killedCreatures 分支，不进 hook。
    //     所以「4 血挨 40 伤」会照常死亡、也不会回血。
    // 官方同类实现可对照遗物 DemonTongue（被击中后 Heal(result.UnblockedDamage)）。
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner)
            return;

        // 伤害被转移给宠物（奥斯提之类）时，真正掉血的是宠物，不该给主人回血
        if (result.Receiver != Owner)
            return;

        // 这一击已经致命（或已死/已归零）就不回血。
        // WasTargetKilled 在「被打到 0 血后又被复活」时依然为 true（引擎注释），
        // 这种也跳过，避免和复活类效果叠成双份治疗。
        if (result.WasTargetKilled || Owner.IsDead || Owner.CurrentHp <= 0)
            return;

        // 只补「真正从生命里扣掉的量」：UnblockedDamage 已扣掉格挡吸收的部分，
        // 过量伤害单独记在 OverkillDamage 里，所以满格挡时不会凭空回血。
        decimal hpLoss = result.UnblockedDamage;
        if (hpLoss <= 0m)
            return;

        Flash();
        await CreatureCmd.Heal(Owner, hpLoss);
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
