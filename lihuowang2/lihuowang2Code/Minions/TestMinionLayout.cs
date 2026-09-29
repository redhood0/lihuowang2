using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MinionLib.Layout;

namespace lihuowang2.Minions;

// 自定义随从布局：整体沿用 MinionLib 的默认摆位，只把「曹操」再往前（离角色更远）推一点。
//
// 机制：MinionLib 的布局是插拔式的 —— MinionLayoutManager.Register(layout, priority)，
// 优先级高的先执行，各布局都往 context.Positions 里写结果，已写过的节点不会再被后面的布局处理。
// 这里把「所有随从」都按默认算法算一遍（保证多随从时的网格间距不重叠），只对自己的随从叠加额外偏移。
public sealed class TestMinionLayout : IMinionLayout
{
    // 额外前移量（像素）：默认前排单人位大约在角色右侧 350px（基础 200 + 网格 150），这里再往外推 120px。
    // 想更远就把 X 调大（例如 200f），想贴近角色就给负值。
    private static readonly Vector2 ExtraForwardOffset = new(120f, 0f);

    public bool IsActive => true;

    public void ApplyLayout(MinionLayoutContext context)
    {
        List<NCreature> minions = context.UnhandledMinions.ToList();
        if (minions.Count == 0)
        {
            return;
        }

        foreach (MinionNodePosition nodePosition in
                 DefaultMinionLayout.CalculateMinionPositions(context.Room, minions))
        {
            Vector2 position = nodePosition.Position;
            if (nodePosition.Node.Entity.Monster is TestMinion)
            {
                position += ExtraForwardOffset;
            }

            context.Positions[nodePosition.Node] = position;
        }
    }
}
