using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MinionLib.Layout;

namespace lihuowang2.Minions;

// 彭龙腾的站位微调：比默认位置再远离玩家角色一点（见下面的 ExtraDistance）。
//
// 坐标规则（MinionLib.Layout.DefaultMinionLayout.CalculateBaseOffset）：
// 玩家在原点附近，Front = (+200, 0)「向前/朝敌人」，Back = (-200, 0)「后方/贴玩家」，
// 也就是说 X 的绝对值就是与玩家的距离。所以「远离玩家」= 顺着它当前所在的那一侧再推出去。
//
// 只处理彭龙腾，其它随从原样留给后面的布局（见 PengLongTeng 静态构造里的注册说明）。
public sealed class PengLongTengLayout : IMinionLayout
{
    // ===== 可调参数 2：额外远离玩家角色的距离（像素）=====
    // 只改这一个数字即可（0 = 回到引擎默认位置）。默认前排基准是 (±200, 0)，
    // 80 ≈ 默认距离的 40%，肉眼能明显看出比别的随从靠外。
    private const float ExtraDistance = 90f;

    public bool IsActive => true;

    public void ApplyLayout(MinionLayoutContext context)
    {
        // 只认领彭龙腾：别的随从（秋吃饱、曹操等）不写进 Positions，
        // 这样它们仍是"未处理"，会继续交给后续布局处理。
        List<NCreature> pengLongTengs = context.UnhandledMinions
            .Where(node => node.Entity.Monster is PengLongTeng)
            .ToList();
        if (pengLongTengs.Count == 0)
        {
            return;
        }

        foreach (MinionNodePosition nodePosition in
                 DefaultMinionLayout.CalculateMinionPositions(context.Room, pengLongTengs))
        {
            Vector2 position = nodePosition.Position;

            // 「远离玩家」必须**相对玩家节点**判断，不能看 X 的正负号：
            // 玩家在房间坐标系里可能位于负 X（本作就是这样），此时"站在玩家前方"的随从 X 也是负的，
            // 用 `position.X >= 0` 会把它误判成"在玩家后方"，于是往玩家那侧推（反方向）。
            // 正确写法：谁的 X 比玩家大就往 +X 推，否则往 -X 推 —— 两个方向都是"离玩家更远"。
            var petOwner = nodePosition.Node.Entity.PetOwner;
            NCreature? ownerNode = (petOwner != null)
                ? context.Room.GetCreatureNode(petOwner.Creature)
                : null;
            float ownerX = ownerNode?.Position.X ?? 0f;
            float direction = position.X < ownerX ? -1f : 1f;

            context.Positions[nodePosition.Node] =
                position + new Vector2(ExtraDistance * direction, 0f);
        }
    }
}
