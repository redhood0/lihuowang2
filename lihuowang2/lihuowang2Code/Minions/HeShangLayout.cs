using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MinionLib.Layout;

namespace lihuowang2.Minions;

// 和尚的站位微调：站在玩家**后方**（Back，由召唤牌指定），并比默认位置**再靠近**玩家角色 20px。
//
// 坐标规则（MinionLib.Layout.DefaultMinionLayout.CalculateBaseOffset）：
// 玩家在原点附近，Front = (+200, 0)「向前/朝敌人」，Back = (-200, 0)「后方/贴玩家」，
// 也就是说 X 的绝对值就是与玩家的距离。
//
// 和「秋吃饱」的对比（同样是后方单位）：
//   · 秋吃饱 QiuChiBaoLayout：ExtraDistance = +20 → 在默认位置外侧 → 距玩家 220px；
//   · 和尚 本布局：CloserDistance = 20  → 在默认位置内侧 → 距玩家 180px（比秋吃饱靠主角 40px）。
// 两个数字想调：改下面的 CloserDistance 即可（调成 0 就是引擎默认的 200px）。
//
// 只处理和尚，其它随从原样留给后面的布局（见 HeShang 静态构造里的注册说明）。
public sealed class HeShangLayout : IMinionLayout
{
    // 相对默认站位（后方基准 = 玩家 -200px）**靠近**玩家角色的距离（像素）。想更贴主角就调大。
    private const float CloserDistance = 10f;

    // ===== 图层（避免和阿秋叠在一起）=====
    // 引擎给"主角 + 所有随从"用的是同一个容器（NCombatRoom.AddCreature → %AllyContainer，
    // 里面**只**放生物节点），绘制顺序 = 子节点顺序（后挂进去的盖在上面）。
    // 也就是说谁先被召唤谁就在下面 —— 而和尚（-180px）与秋吃饱（-220px）立绘横向重叠一百多像素，
    // 后召唤的那只就会压在另一只身上。
    //
    // 做法：把和尚挪到该容器的**第 0 位**（最底层），于是 z 相同的兄弟（主角、秋吃饱、其它随从）
    // 都会画在它上面，与召唤顺序无关。
    //
    // ⚠ 别用 ZIndex 来干这件事：房间自己的几个容器带的是**负** z（SceneContainer = -10、
    // CombatVfxContainer = -9），Godot 的 ZAsRelative 又默认 true，给生物设 ZIndex = -1 会被
    // 那一整片背景直接盖住 —— 实测和尚会整个看不见。子节点顺序则不会跨容器，安全。
    // 想反过来让和尚盖住秋吃饱：把下面的 MoveToBackIndex 换成 -1（Node.MoveChild 的 -1 = 移到末尾）。
    private const int MoveToBackIndex = 0;

    public bool IsActive => true;

    public void ApplyLayout(MinionLayoutContext context)
    {
        // 只认领和尚：别的随从（秋吃饱、彭龙腾、曹操等）不写进 Positions，
        // 这样它们仍是"未处理"，会继续交给后续布局（它们各自的布局 / TestMinionLayout）处理。
        List<NCreature> heShangs = context.UnhandledMinions
            .Where(node => node.Entity.Monster is HeShang)
            .ToList();
        if (heShangs.Count == 0)
        {
            return;
        }

        foreach (MinionNodePosition nodePosition in
                 DefaultMinionLayout.CalculateMinionPositions(context.Room, heShangs))
        {
            NCreature node = nodePosition.Node;
            Vector2 position = nodePosition.Position;

            // 图层：每次算布阵都把和尚挪回最底层（幂等）。放这里的意义是"跟着布阵一起刷新"——
            // 召唤（AddMinion 末尾的 Rearrange）与重连后重建生物节点（引擎 AddCreature 的补丁）都会
            // 重新走一遍布局，所以重连/读档回来也不会又跑到秋吃饱上面。
            // MoveChildSafely 在非主线程/节点未就绪时会自动改用 CallDeferred，是引擎自己调序用的那套。
            node.GetParent()?.MoveChildSafely(node, MoveToBackIndex);

            // 「靠近/远离玩家」必须**相对玩家节点**判断，不能看 X 的正负号：
            // 玩家在房间坐标系里可能位于负 X（本作就是这样），用 `position.X >= 0` 会判错方向。
            // 写法：谁的 X 比玩家大（在玩家前方）就认为 direction = +1，否则（在后方）为 -1；
            // 减去 CloserDistance * direction = 无论站在哪一侧，都是"朝玩家挪近"。
            var petOwner = nodePosition.Node.Entity.PetOwner;
            NCreature? ownerNode = (petOwner != null)
                ? context.Room.GetCreatureNode(petOwner.Creature)
                : null;
            float ownerX = ownerNode?.Position.X ?? 0f;
            float direction = position.X < ownerX ? -1f : 1f;

            context.Positions[nodePosition.Node] =
                position - new Vector2(CloserDistance * direction, 0f);
        }
    }
}
