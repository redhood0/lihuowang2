// ===== 暂时停用（尸爆）=====
// 幽灵环（游老爷）已不再施加此 power，卡面说明里也没有它；这里整块注释掉，power 不会被注册进游戏。
// 后面要用时：去掉下面块的注释，并同步恢复 lihuowang2GhostRing.cs 里的 Apply 调用（含 lihuowang2.Powers 的 using）即可。
/*

using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 尸爆（幽灵环施加给敌人的死亡触发 debuff）：
// 持有者死亡时，对其余敌人造成等于其最大生命值的伤害。
[RegisterPower]
public class Lihuowang2CorpseExplosionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标暂用「魍魉」占位（幽灵系视觉近似）
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/wangliang32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/wangliang84.png");

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        // 只有携带者本人阵亡才触发（被免死救回时不算）
        if (creature == Owner && !wasRemovalPrevented)
        {
            decimal burstDamage = creature.MaxHp;
            // 快照遍历：边遍历边造成伤害会把死亡的敌人移出 Enemies，导致枚举异常
            foreach (Creature enemy in Owner.CombatState!.Enemies.ToArray())
            {
                if (enemy == creature || enemy.IsDead)
                    continue;
                await CreatureCmd.Damage(choiceContext, enemy, burstDamage,
                    ValueProp.Unblockable, creature, null, null);
            }
        }

        await base.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength);
    }
}

*/
