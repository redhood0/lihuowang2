using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MinionLib.Layout;

namespace lihuowang2.Minions;

// 随从「召唤台词气泡」的共用实现：秋吃饱与彭龙腾都走这里，各传自己的台词与"往哪边摊开"。
//
// 两个关键点（都在引擎源码里核对过）：
//
// 1) 气泡往哪边摊开由 DialogueSide 决定，而引擎 NSpeechBubbleVfx._Ready 里**只有 DialogueSide.Right**
//    会把气泡本体镜像到锚点左侧（_container.Position = -width…、_bubble/_shadow.FlipH = true）：
//      · DialogueSide.Left  → 气泡从锚点向**右**摊开（本类的 extendRight: true）
//      · DialogueSide.Right → 气泡从锚点向**左**摊开（本类的 extendRight: false）
//    不能用 Create(text, speaker, …) 那个按阵营自动选的重载：它对玩家阵营固定给 Left（向右摊开），
//    对站在主角左手边的随从来说正好盖在主角头上 → 看着像主角在说话。
//
// 2) 坐标只在入树那一刻的 _Ready 里应用一次（_Process 只做轻微上下浮动，不跟随说话者），
//    所以 Create(...) 造好节点后**必须自己 AddChild**（引擎的 TalkCmd.Play 也是调用方负责挂的），
//    挂完还能再挪位置。
internal static class MinionSpeechBubble
{
    // 气泡离屏幕边缘的最小距离：贴边时整体挪进来，免得被裁掉。
    private const float ScreenMargin = 8f;

    /// <summary>
    /// 在随从头顶说一句台词（对话气泡）。
    /// </summary>
    /// <param name="speaker">说话者（随从生物）。已死 / 没有战斗节点时不播。</param>
    /// <param name="locTable">本地化表名（随从一般用 "monsters"）。</param>
    /// <param name="locKey">台词键。</param>
    /// <param name="color">气泡配色（只影响配色）。</param>
    /// <param name="seconds">停留时长（秒）。</param>
    /// <param name="offset">相对"头顶中点"的微调（像素；X 右为正 / Y 下为正）。</param>
    /// <param name="extendRight">
    /// true  = 气泡从锚点向**右**摊开（随从站在玩家**前方**时用，例如彭龙腾）；
    /// false = 气泡从锚点向**左**摊开（随从站在玩家**后方**时用，例如秋吃饱）。
    /// 选错方向的后果很直观：气泡会盖在主角头上，看起来像主角在说话。
    /// </param>
    public static void Play(Creature speaker, string locTable, string locKey,
        VfxColor color, double seconds, Vector2 offset, bool extendRight)
    {
        if (speaker.IsDead)
            return;

        NCreature? node = speaker.GetCreatureNode();
        if (node == null)   // 图鉴 / 测试模式等没有战斗节点的情况
            return;

        Control? container = speaker.GetVfxContainer();   // 引擎侧挂在 Creature 上：战斗特效层
        if (container == null)
            return;

        NSpeechBubbleVfx? bubble = NSpeechBubbleVfx.Create(
            new LocString(locTable, locKey).GetFormattedText(),
            extendRight ? DialogueSide.Left : DialogueSide.Right,
            GetHeadTopGlobalPosition(node) + offset,
            seconds,
            color);

        if (bubble == null)
            return;

        container.AddChildSafely(bubble);
        KeepOnScreen(bubble, extendRight);
    }

    // 挂点 = 随从**最终站位**的"头顶中点"：
    //   · 优先用官方视觉自带的「%TalkPos」标记（我们的贴图视觉没有这个标记，所以走下面）；
    //   · 否则取包围盒上沿的中点 —— 横向居中、纵向贴顶，换贴图/改尺寸都不用重调坐标。
    //
    // 用"最终站位"而不是"节点当前位置"：摆位 MinionAnimCmd.Rearrange 是在 OnSummon 之后才起的
    // 0.25 秒补间，刚召唤那一瞬间节点还贴在主角身上；而且极速(Instant)模式下 Cmd.Wait 会被跳过，
    // 单纯"等一会儿"并不可靠 —— 所以直接问布局要目标站位。
    private static Vector2 GetHeadTopGlobalPosition(NCreature node)
    {
        if (node.Visuals?.TalkPosition != null)
        {
            return node.Visuals.TalkPosition.GlobalPosition;
        }

        // 头顶相对"节点原点"的偏移：只用包围盒尺寸与位置差，跟节点当前在哪无关。
        Vector2 headOffset = Vector2.Zero;
        Rect2 box = node.Hitbox.GetGlobalRect();
        if (box.Size.X > 0f && box.Size.Y > 0f)
        {
            headOffset = new Vector2(0f, box.Position.Y - node.GlobalPosition.Y);
        }

        return GetTargetSlotGlobalPosition(node) + headOffset;
    }

    // 随从**最终站位**的全局坐标（布局算出来的目标位置）。
    private static Vector2 GetTargetSlotGlobalPosition(NCreature node)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room != null)
        {
            foreach (MinionNodePosition target in MinionLayoutManager.CalculateLayout(room))
            {
                if (target.Node == node)
                {
                    // 布局给的是 Control.Position（相对父节点）。父节点不动，所以
                    // "当前 GlobalPosition - 当前 Position" 就是父节点的全局偏移，拿它换算即可。
                    return target.Position + (node.GlobalPosition - node.Position);
                }
            }
        }

        return node.GlobalPosition;   // 兜底：布局没认领它（例如图鉴里）
    }

    // 别把气泡顶出画面。锚点就是气泡的"尾巴尖"，所以：
    //   · 向左摊开（extendRight=false）→ 看锚点自己的 X（气泡整体在锚点左侧）；
    //   · 向右摊开（extendRight=true）→ 看「锚点 + 气泡宽度」（宽度还没算出来时不动，
    //     宁可不调也不要误判 —— Control.Size 在 Label 文本设置前可能还是 0）。
    private static void KeepOnScreen(NSpeechBubbleVfx bubble, bool extendRight)
    {
        Vector2 position = bubble.GlobalPosition;

        if (!extendRight)
        {
            if (position.X < ScreenMargin)
            {
                bubble.GlobalPosition = new Vector2(ScreenMargin, position.Y);
            }
            return;
        }

        Viewport? viewport = bubble.GetViewport();
        float viewportWidth = viewport?.GetVisibleRect().Size.X ?? 0f;
        float bubbleWidth = bubble.Size.X;
        if (viewportWidth <= 0f || bubbleWidth <= 0f)
        {
            return;
        }

        float rightEdge = position.X + bubbleWidth;
        if (rightEdge > viewportWidth - ScreenMargin)
        {
            bubble.GlobalPosition = new Vector2(
                viewportWidth - ScreenMargin - bubbleWidth, position.Y);
        }
    }
}
