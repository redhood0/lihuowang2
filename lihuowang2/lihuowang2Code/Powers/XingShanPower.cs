using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
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

// 行善（随从「和尚」身上的能力）：
// 召唤者的回合开始时，随机移除一个**友方角色**身上的一层负面状态（并在那个角色身上播官方特效），
// 然后和尚自己失去 1 点生命 —— 做好事是要付出代价的（所以和尚的 6 点血是会被慢慢耗掉的）。
//
// 「友方」只算**友方角色**（玩家）：
//   · 自己（召唤者）；
//   · **双人模式里的另一个队友**（也是玩家，同样会被照顾到）。
// **不解随从身上的任何东西** —— 随从身上的负面状态（例如和尚自己那份被引擎标成 Debuff 的
// 「先天一炁」）一律不进候选表（见下面的 ally.IsPlayer 过滤）。
//
// 随机取的是「(友方角色, 负面状态)」这一**对**：先把整张候选表摊平再抽一个 ——
// 这样负面状态多的角色被抽中的概率更高，但不会把随机额度浪费在没有负面状态的角色身上
//（否则"随机选一个角色，再从他身上随便挑一个"，常常会白掉一次）。
[RegisterPower]
public class XingShanPower : ModPowerTemplate
{
    // ===== 可调参数 =====
    // 每次行善的代价：和尚自己失去几点生命（Unblockable = 无视格挡的真实生命损失）。
    private const decimal SelfHpCost = 1m;

    // 被解掉负面状态的那个角色身上播的官方特效（想换观感只改这一行，都是引擎现成资源）：
    //   VfxCmd.healPath（"vfx/vfx_cross_heal"，十字圣光，当前）
    //   VfxCmd.adrenalinePath（"vfx/vfx_adrenaline"，振奋）  "vfx/vfx_starry_impact"（星辉）
    private const string CleanseVfxPath = VfxCmd.healPath;

    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // 不吃层数：每回合固定解 1 层，层数不影响强度（图标上也不显示数字）。
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标：复用官方「肉盾」（TankPower）的图标 —— 与模组「唯一」借用官方「爪牙」图标同一套做法：
    // 直接从官方模型拿路径，不手写/猜资源名
    // （IconPath = 图标行用的小图；ResolvedBigIconPath = 大图，缺失时引擎会自己回落到占位图）。
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel tank = ModelDb.Power<TankPower>();
            return new PowerAssetProfile(
                IconPath: tank.IconPath,
                BigIconPath: tank.ResolvedBigIconPath);
        }
    }

    // 回合开始：只认召唤者本人的那一次（与「将相首」完全同一套写法）——
    // 双人模式下 Hook.PlayerTurnStart 会把每个玩家的回合开始都通知一遍，
    // 不筛的话队友的回合也会替我们多解一次。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.PetOwner != player || Owner.IsDead)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        await CleanseOneDebuffStackAsync(choiceContext, player);

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 随机解掉一层负面状态。
    private async Task CleanseOneDebuffStackAsync(PlayerChoiceContext choiceContext, Player summoner)
    {
        // 先摊平候选：每个"友方 × 其身上的负面状态"。
        // 注意必须先收集再结算：PowerCmd.Decrement 会让层数归零的能力从列表里消失，
        // 边遍历边改会踩到枚举异常。
        List<PowerModel> candidates = new();
        foreach (Creature ally in Owner.CombatState!.Allies)
        {
            // 只认「友方角色」（玩家本人 + 双人模式里的队友）。
            // IsPlayer 会把随从全部排除掉 —— 所以随从身上的负面状态（含和尚自己的「先天一炁」）
            // 永远不会被解掉。
            if (!ally.IsPlayer || !ally.IsAlive)
            {
                continue;
            }

            foreach (PowerModel power in ally.Powers)
            {
                if (power.Type == PowerType.Debuff && power.Amount > 0)
                {
                    candidates.Add(power);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return;   // 全场没有一个负面状态：这一回合就白攒了（不提示、不消耗任何东西）
        }

        // 随机必须走引擎的确定性随机流（CombatTargets = 战斗内随机选目标），
        // 否则联机两边解掉的不是同一个状态，会直接触发状态校验分歧。
        PowerModel? chosen = summoner.RunState.Rng.CombatTargets.NextItem(candidates);
        if (chosen == null)
        {
            return;
        }

        // 先记下"被解的那个角色"：这条能力可能因为层数归零在下面被移除，
        // 移除后不要再回头读 chosen.Owner。
        Creature cleansed = chosen.Owner;

        Flash();

        // -1 层；层数归零时引擎会自己把该能力移除
        //（PowerCmd.ModifyAmount → ShouldRemoveDueToAmount → Remove）。
        await PowerCmd.Decrement(chosen);

        // 那个角色身上播一下官方特效（引擎自带"在生物身上播特效"的接口：位置取该生物的 VfxSpawnPosition，
        // 特效容器也挂在它自己身上，所以随从/玩家/队友都能正确播到）。
        VfxCmd.PlayOnCreatureCenter(cleansed, CleanseVfxPath);

        // 代价：和尚自己失去 1 点生命。
        // Unblockable = 无视格挡的真实生命损失，与模组「大千录」系自伤同一套写法
        //（dealer 传自己，头部战斗记录里也会显示成"自己造成"）。血不够就是直接死。
        await CreatureCmd.Damage(choiceContext, Owner, SelfHpCost, ValueProp.Unblockable,
            Owner, null, null);
    }
}
