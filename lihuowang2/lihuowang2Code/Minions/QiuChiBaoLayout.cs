using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MinionLib.Layout;

namespace lihuowang2.Minions;

// 秋吃饱的站位微调：比默认位置再远离玩家角色 20px。
//
// 坐标规则（MinionLib.Layout.DefaultMinionLayout.CalculateBaseOffset）：
// 玩家在原点附近，Front = (+200, 0)「向前/朝敌人」，Back = (-200, 0)「后方/贴玩家」，
// 也就是说 X 的绝对值就是与玩家的距离。所以「远离玩家」= 顺着它当前所在的那一侧再推出去。
//
// 只处理秋吃饱，其它随从原样留给后面的布局（见 QiuChiBao 静态构造里的注册说明）。
public sealed class QiuChiBaoLayout : IMinionLayout
{
    // 额外远离玩家角色的距离（像素）。想更远就调大。
    private const float ExtraDistance = 20f;

    public bool IsActive => true;

    public void ApplyLayout(MinionLayoutContext context)
    {
        // 只认领秋吃饱：别的随从（例如曹操）不写进 Positions，
        // 这样它们仍是"未处理"，会继续交给后续布局（TestMinionLayout 等）处理。
        List<NCreature> qiuChiBaos = context.UnhandledMinions
            .Where(node => node.Entity.Monster is QiuChiBao)
            .ToList();
        if (qiuChiBaos.Count == 0)
        {
            return;
        }

        foreach (MinionNodePosition nodePosition in
                 DefaultMinionLayout.CalculateMinionPositions(context.Room, qiuChiBaos))
        {
            Vector2 position = nodePosition.Position;

            // 玩家在原点：X >= 0 说明随从在玩家前面，往 +X 推；否则在后方，往 -X 推。
            // 两个方向都是"离玩家更远"。
            float direction = position.X >= 0f ? 1f : -1f;
            context.Positions[nodePosition.Node] =
                position + new Vector2(ExtraDistance * direction, 0f);
        }
    }
}
