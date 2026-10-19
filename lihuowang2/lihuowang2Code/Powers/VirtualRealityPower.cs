using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 虚拟？现实（能力牌「虚拟？现实」施加）：每当你预见，对一个随机敌人造成 Amount 点伤害。
//
// 触发点在 Cards/Lihuowang2ScryUtil.cs 的 ScryAndDiscardAny 末尾（IForeseeListener），
// 所以所有「预见」来源都会走到：卡牌「关口」「逐卜避荒」、遗物「瓶娃」的右键预见。
[RegisterPower]
public class VirtualRealityPower : ModPowerTemplate, IForeseeListener
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // Counter：层数就是"每次预见造成的伤害"，图标上直接显示（初始 6）。
    // 再次打出这张牌会叠加 → 每次预见打得更疼。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标：复用官方能力「劫难」(CalamityPower) 的图标。
    // 直接从官方模型拿路径，不手写资源名：
    //   IconPath            = 图集里的小图标（图标行用）
    //   ResolvedBigIconPath = 官方大图
    // 等专属图做好了，换成 $"{Entry.ResPath}/images/powers/xxx.png" 即可。
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel calamity = ModelDb.Power<CalamityPower>();
            return new PowerAssetProfile(
                IconPath: calamity.IconPath,
                BigIconPath: calamity.ResolvedBigIconPath);
        }
    }

    public Task OnForesee(PlayerChoiceContext choiceContext, Player player)
    {
        return StrikeRandomEnemy(player);
    }

    // 随机一个「可命中」的敌人吃 Amount 点伤害。
    // 随机目标必须走引擎的确定性随机流（CombatTargets），否则联机两端选中的敌人不一致。
    private async Task StrikeRandomEnemy(Player player)
    {
        // 不在战斗里就直接跳过（预见的来源含遗物右键，防御性判断，避免 CombatState 为 null）。
        if (Owner.CombatState is not { } combatState)
        {
            return;
        }

        Creature? target = player.RunState.Rng.CombatTargets
            .NextItem(combatState.HittableEnemies);
        if (target == null)
        {
            return;   // 场上没有可命中的敌人：跳过，不报错
        }

        // 与官方「黑洞」(BlackHolePower.DealDamageToAllEnemies) 对齐的三点：
        //   1. Flash()：触发时能力图标闪一下，玩家能看出是这张牌打的；
        //   2. BlockingPlayerChoiceContext：用阻塞式上下文，动画/结算期间锁住输入；
        //   3. ValueProp.Unpowered：固定伤害，不吃力量/易伤等一切修正（卡面写 6 就是 6）。
        Flash();
        await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), target, Amount,
            ValueProp.Unpowered, Owner, null, null);
    }
}
