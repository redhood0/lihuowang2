using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 诵经（随从「和尚」身上的能力）：层数 = 积攒（召唤时自带 InitialStacks = 1，上限见 MaxStacks = 10）。
//
// 攒层有两条路，都走同一个 Advance()：
//   · 召唤者的回合结束时 +1（多人时只认召唤者本人的那一次，与「石渎」同一套判断）；
//   · 场上已有活着的和尚时，再打出「修真·和尚」→ 那只和尚身上的诵经也 +1（见 AdvanceExisting）。
// 每 +1 层都按新层数结算：
//   · 攒到 MaxStacks（10）→ 说「噫，不曾想我竟是世尊~」，对随机一个敌人砸 999 点（带官方现成的特效），
//     随后层数回到 InitialStacks（1）层、重新开始攒（能力不消失）；
//   · 还没到 → 随机念一句（善哉善哉 / 阿弥陀佛）。
//
// 关于初始层数：不能给 0 层 —— 引擎对 0 层的 Counter 能力会立刻移除
//（PowerModel.ShouldRemoveDueToAmount 是**非虚**方法，改不了），PowerCmd.Apply 传 0 层也是空操作。
// 所以召唤时由 HeShang 施加 InitialStacks（1）层。
[RegisterPower]
public class SongJingPower : ModPowerTemplate
{
    // 召唤时自带的层数。（必须是正数：0 层的能力在引擎里留不住，见上面的说明。）
    public const int InitialStacks = 1;

    // 攒到这一层就"成佛"：说台词 + 砸 999 点，然后回到 InitialStacks（1）层重新攒。
    public const int MaxStacks = 10;

    // 收尾特效：直接复用**官方现成**的 VFX（路径都在引擎 VfxCmd 的常量里，不自己造资源）。
    // 当前用的是"天雷"打在目标身上；想换观感只改这一行，同为官方现成的候选：
    //   VfxCmd.heavyBluntPath（重钝击）      VfxCmd.starryImpactVfx（星辉冲击）
    //   VfxCmd.lightningPath（天雷，当前）    "vfx/vfx_hyperbeam_impact"（光束冲击）
    //   "vfx/vfx_grand_finale_impact"（盛大终章）
    private const string FinaleVfxPath = VfxCmd.lightningPath;

    // 收尾那一下的伤害。
    private const decimal FinalDamage = 999m;

    // 收尾台词与伤害之间的停顿（秒）：让台词先露个脸，再砸下去。
    private const float FinalePauseSeconds = 0.4f;

    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // Counter = 图标上显示层数，且层数只由代码手动增减（引擎不会自己 tick）。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标：images/powers/heshang.png（同一张图兼作小/大图标）
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/heshang.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/heshang.png");

    // 召唤者的回合结束时 +1 层。用 AfterSideTurnEnd + side/participants 双重判断
    //（与「石渎」同一套写法）：多人时每个玩家各有一个回合结束，只认召唤者本人的那一次。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        Player? summoner = Owner.PetOwner;
        if (side != CombatSide.Player || summoner == null ||
            !participants.Contains(summoner.Creature))
        {
            await base.AfterSideTurnEnd(choiceContext, side, participants);
            return;
        }

        await Advance(choiceContext, summoner, cardSource: null);

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }

    /// <summary>
    /// 卡牌「修真·和尚」走"已有随从、只加血"那条路时调用：让那只和尚的诵经也 +1 层
    ///（到 MaxStacks 就照常结算收尾）。
    /// </summary>
    public static async Task AdvanceExisting(PlayerChoiceContext choiceContext, Player summoner,
        CardModel? cardSource)
    {
        Creature? minion = summoner.PlayerCombatState?.GetPet<HeShang>();
        if (minion == null || !minion.IsAlive)
        {
            return;
        }

        SongJingPower? chant = minion.GetPower<SongJingPower>();
        if (chant != null)
        {
            await chant.Advance(choiceContext, summoner, cardSource);
            return;
        }

        // 兜底：那只和尚身上没有诵经（只有老存档 / 施加失败这类异常才走到），按 InitialStacks 挂上。
        // 1 层不可能触发收尾，所以这里只需要接上"随机念一句"。
        await PowerCmd.Apply<SongJingPower>(choiceContext, minion, InitialStacks, minion, cardSource);
        HeShang.PlayChantLine(minion, summoner);
    }

    // +1 层，然后按新层数结算：到顶 = 收尾（台词 + 999），没到 = 随机念一句。
    private async Task Advance(PlayerChoiceContext choiceContext, Player summoner,
        CardModel? cardSource)
    {
        Flash();

        await PowerCmd.ModifyAmount(choiceContext, this, 1m, Owner, cardSource);

        if (Amount >= MaxStacks)
        {
            // 攒满：层数先回到 1 层（重新开始攒，能力不消失），再说话、再砸。
            await PowerCmd.ModifyAmount(choiceContext, this, InitialStacks - Amount, Owner, cardSource);

            HeShang.PlayFinaleLine(Owner);
            await Cmd.Wait(FinalePauseSeconds);
            await StrikeRandomEnemy(choiceContext, summoner);
            return;
        }

        HeShang.PlayChantLine(Owner, summoner);
    }

    // 随机一个「可命中」的敌人，吃 999 点伤害。
    //
    // 固定伤害 = Move | Unpowered：跳过力量 / 易伤 / 虚弱等一切伤害修正，数字恒为 999（不受召唤者力量影响）。
    // 仍然会被[格挡]挡下 —— 想连格挡一起打穿，把 props 换成 ValueProp.Unblockable 即可。
    private async Task StrikeRandomEnemy(PlayerChoiceContext choiceContext, Player summoner)
    {
        // 随机目标必须走引擎的确定性随机流（CombatTargets = 战斗内随机选目标），
        // 否则联机两边选中的敌人不同，会直接触发状态校验分歧。
        Creature? target = summoner.RunState.Rng.CombatTargets
            .NextItem(Owner.CombatState!.HittableEnemies);
        if (target == null)
        {
            return;   // 场上没有可命中的敌人：台词照说、特效不播、伤害跳过
        }

        PlayFinaleVfx(target);

        await CreatureCmd.Damage(choiceContext, target, FinalDamage,
            ValueProp.Move | ValueProp.Unpowered, Owner, null, null);
    }

    // 在目标身上播一下官方现成的收尾特效（路径见 FinaleVfxPath）。
    // 位置取目标战斗节点的**身体中部**：生物节点原点在脚底，往上抬半个包围盒高度，
    // 免得天雷贴着地面炸。拿不到节点（图鉴 / 测试模式）就静默不播，不影响伤害结算。
    private static void PlayFinaleVfx(Creature target)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        NCreature? node = room?.GetCreatureNode(target);
        if (node == null)
        {
            return;
        }

        Vector2 position = node.GlobalPosition;
        Rect2 box = node.Hitbox.GetGlobalRect();
        if (box.Size.Y > 0f)
        {
            position.Y -= box.Size.Y * 0.5f;
        }

        VfxCmd.PlayVfx(position, FinaleVfxPath, room?.CombatVfxContainer);
    }
}
