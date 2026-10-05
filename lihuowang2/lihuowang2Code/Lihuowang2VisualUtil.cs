using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace lihuowang2;

// 战斗人物形象的运行时替换工具。两套做法：
//
// ① 换贴图（SetBodyTexture）：改 %Visuals 那张 Sprite2D 的 Texture —— 适合模组自绘的角色图。
//    原理：自定义角色的战斗场景（lihuowang2_character.tscn）本身就是 NCreatureVisuals 本体，
//    引擎在 NCreatureVisuals._Ready() 里执行 _body = GetNode<Node2D>("%Visuals")。
//
// ② 换整份怪物形象（SetBodyMonsterVisuals）：把游戏本体的怪物 Spine 场景挂到玩家身上 —— 会动。
//    引擎装配一个单位的形象是三步（见 NCreature._Ready）：
//      · entity.CreateVisuals()                     实例化 creature_visuals/<id>.tscn
//      · monster.GenerateAnimator(visuals.SpineBody) 建动画器（idle/attack/die 状态机）
//      · visuals.SetUpSkin(monster)                  上皮肤
//    我们照抄这三步，把怪物形象作为新子节点挂到玩家的 NCreature 上，并把原形象隐藏。
//    ⚠ NCreature.Visuals 是 private set，换不掉，所以用"叠加 + 隐藏旧的"：
//      血量条 / 命中框 / 意图位置仍然来自原形象（引擎的 UpdateBounds 读的就是 Visuals），操作不受影响。
//
// ③ 「变脸」是"临时外观"，结束时要变回**变脸前那一刻**的身体形象：
//      · 只是默认图 → 变回默认图；
//      · 之前正处在黑太岁形态 → 变回黑太岁形态（而不是一律回默认）。
//    实现：CurrentBodyPaths 记录"身体本身现在应该是什么样"（默认图 / 黑太岁形态 / …，
//    由改图的那一方负责登记）；变脸开始时用 CaptureBodyForFaceForm 拍一张快照，
//    结束（能力被移除）时用 RestoreBodyAfterFaceForm 按快照还原。
//    这样四种先后顺序都对：默认→变脸、黑太岁→变脸、变脸→黑太岁、变脸→黑太岁→黑太岁被移除。
//
// 注意：
//  * 判定框 / 特效锚点来自场景里写死的 %Bounds、%CenterPos、%IntentPos，不会跟着形象变，
//    所以新形象最好和原图构图接近（脚底位置、整体大小）。需要时用 scale/position 参数微调。
//  * 每个客户端各自持有战斗场景，所以换形象只影响本机显示；新战斗开始时场景重新实例化，自动恢复默认。
public static class Lihuowang2VisualUtil
{
    // 默认形象。
    public const string DefaultBodyPath = $"{Entry.ResPath}/images/characters/lhw_character.png";

    // 「黑太岁」转化形象。
    public const string HeitaisuiBodyPath = $"{Entry.ResPath}/images/characters/lhw_trans01.png";

    // 记录每个身体节点第一次被改图前的原始状态，还原时精确恢复（多人下每个角色各一份）。
    private static readonly Dictionary<ulong, BodyState> OriginalStates = [];

    // 每个 NCreature 当前挂着的"怪物形象"节点与动画器（按 NCreature 的 instance id 记）。
    private static readonly Dictionary<ulong, MonsterVisualState> MonsterVisuals = [];

    // 「身体本身现在应该是什么样」——贴图路径（默认图 / 黑太岁形态 / …）。
    // 换贴图的那一方负责登记；变脸只借用它拍快照，不算自己改了身体。
    private static readonly Dictionary<ulong, string> CurrentBodyPaths = [];

    // 「变脸开始前那具身体」的快照（按 NCreature 的 instance id 记），变脸结束时按它还原。
    private static readonly Dictionary<ulong, BodyState> SavedBodyStates = [];

    // 「变脸正在进行中」的标记：决定 CaptureBodyForFaceForm 是"保留最初快照"还是"重新拍一张"。
    private static readonly HashSet<ulong> FaceFormActive = [];

    private struct BodyState
    {
        public Texture2D? Texture;
        public Vector2 Scale;
        public Vector2 Position;

        // 快照用：要还原成哪张图（res:// 路径）。为 null 表示这份记录不是给变脸用的。
        public string? Path;
    }

    private sealed class MonsterVisualState
    {
        public required NCreature Node;
        public required NCreatureVisuals Visuals;

        // 动画器是普通类（不是 Godot 节点），必须留着引用，否则会被回收、形象就不动了。
        // 延迟入树的情况下要等 _Ready 之后才能建，所以是可空的。
        public CreatureAnimator? Animator;
    }

    /// <summary>把某个战斗单位的形象换成指定贴图。返回是否成功。</summary>
    /// <param name="creature">要换形象的单位（通常是 Owner.Creature）。</param>
    /// <param name="texturePath">res:// 开头的贴图路径，必须在 pck 里。</param>
    /// <param name="scale">可选的缩放倍率，null = 不改动。</param>
    /// <param name="position">可选的局部坐标，null = 不改动。</param>
    public static bool SetBodyTexture(Creature? creature, string texturePath,
        float? scale = null, Vector2? position = null)
    {
        // 拿不到节点说明当前不在战斗场景里（例如卡牌图鉴、事件里的预览），静默跳过。
        if (GetBody(creature) is not Sprite2D sprite)
            return false;

        if (!ResourceLoader.Exists(texturePath))
        {
            Entry.Logger.Warn($"[lihuowang2] 形象贴图不存在：{texturePath}");
            return false;
        }

        // 换贴图前先把"怪物形象"摘掉，避免两套形象叠在一起；顺带恢复原形象的可见性。
        ClearMonsterVisuals(creature);

        // 第一次改这张图前，先记下原样。
        if (!OriginalStates.ContainsKey(sprite.GetInstanceId()))
        {
            OriginalStates[sprite.GetInstanceId()] = new BodyState
            {
                Texture = sprite.Texture,
                Scale = sprite.Scale,
                Position = sprite.Position
            };
        }

        sprite.Texture = ResourceLoader.Load<Texture2D>(texturePath);

        if (scale.HasValue)
            sprite.Scale = Vector2.One * scale.Value;
        if (position.HasValue)
            sprite.Position = position.Value;

        // 登记"身体现在长这样"：变脸结束时若还处于这个形态，就要还原成这一张。
        SetCurrentBodyPath(creature, texturePath);

        return true;
    }

    /// <summary>
    /// 把某个战斗单位的形象换成"游戏本体的怪物形象"（整份 Spine 场景，会动）。返回是否成功。
    /// </summary>
    /// <param name="creature">要换形象的单位（通常是 Owner.Creature）。</param>
    /// <param name="monster">要变身的怪物（用它的配置与形象场景）。</param>
    /// <param name="scale">可选的缩放倍率，null = 不改动。</param>
    /// <param name="position">可选的局部坐标，null = 用 Vector2.Zero（与引擎装配单位形象时一致）。</param>
    public static bool SetBodyMonsterVisuals(Creature? creature, MonsterModel monster,
        float? scale = null, Vector2? position = null)
    {
        // 拿不到节点说明当前不在战斗场景里（例如卡牌图鉴、事件里的预览），静默跳过。
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return false;

        // 先清掉旧的（重复打出变脸 / 换形态时不会叠两套）
        ClearMonsterVisuals(creature);

        NCreatureVisuals visuals;
        try
        {
            visuals = monster.CreateVisuals();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[lihuowang2] 变脸：加载 {monster.Id.Entry} 的形象场景失败：{ex.Message}");
            return false;
        }

        // 挂到玩家身上（原形象隐藏，血量条/命中框仍由它提供）。
        // AddChildSafely 在非主线程/父节点未就绪时会延迟到下一帧，所以 _Ready 之后再装配骨架与动画器。
        node.AddChildSafely(visuals);
        node.MoveChildSafely(visuals, 0);
        visuals.Position = position ?? Vector2.Zero;

        // 水平翻转：怪物形象是"站在右侧、面朝玩家（朝左）"设计的，直接挂到玩家身上会背对敌人，
        // 所以 X 轴取负 → 面朝敌人（玩家在左侧、敌人朝右）。
        float bodyScale = scale ?? 1f;
        visuals.Scale = new Vector2(-bodyScale, bodyScale);

        MonsterVisualState state = new() { Node = node, Visuals = visuals };
        MonsterVisuals[node.GetInstanceId()] = state;

        // 隐藏原形象（血量条/命中框/意图锚点都还在，只有画面被藏起来）。
        node.Visuals.Visible = false;

        if (visuals.IsNodeReady())
            AttachSkeletonAndAnimator(state, monster);
        else
            visuals.Ready += () => AttachSkeletonAndAnimator(state, monster);

        return true;
    }

    // 引擎装配怪物形象的第二步与第三步：上皮肤 + 建动画器（照抄 NCreature._Ready）。
    private static void AttachSkeletonAndAnimator(MonsterVisualState state, MonsterModel monster)
    {
        if (!GodotObject.IsInstanceValid(state.Visuals))
            return;

        MegaSprite? spine = state.Visuals.SpineBody;
        if (spine == null)
            return;   // 这个怪物形象不是 Spine 骨骼（极少见），那就只当静态形象显示

        try
        {
            state.Visuals.SetUpSkin(monster);
            state.Animator = monster.GenerateAnimator(spine);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[lihuowang2] 变脸：装配 {monster.Id.Entry} 的动画失败：{ex.Message}");
        }
    }

    // ---------- 「当前身体形象」的登记与读取 ----------

    /// <summary>取「这具身体现在应该是什么样」的贴图路径（没登记过就是默认图）。</summary>
    public static string GetCurrentBodyPath(Creature? creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return DefaultBodyPath;

        return CurrentBodyPaths.TryGetValue(node.GetInstanceId(), out string? path) &&
               !string.IsNullOrEmpty(path)
            ? path
            : DefaultBodyPath;
    }

    /// <summary>
    /// 登记「这具身体现在应该是什么样」。换贴图的那一方调用；
    /// 若此刻正挂着变脸（有快照），快照会一并更新 —— 这样"变脸期间换了形态"结束时也能回到新形态。
    /// </summary>
    public static void SetCurrentBodyPath(Creature? creature, string path)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return;

        ulong key = node.GetInstanceId();
        CurrentBodyPaths[key] = path;

        if (SavedBodyStates.TryGetValue(key, out BodyState saved))
        {
            saved.Path = path;
            SavedBodyStates[key] = saved;
        }
    }

    // ---------- 变脸：拍快照 / 按快照还原 ----------

    /// <summary>
    /// 变脸开始前拍一张快照：记下"这具身体现在长什么样"（默认图 / 黑太岁形态 / …）。
    /// 重复打出变脸不会覆盖最初的快照。
    /// </summary>
    public static void CaptureBodyForFaceForm(Creature? creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return;

        ulong key = node.GetInstanceId();
        if (FaceFormActive.Contains(key))
            return;   // 变脸还在持续中（重复打出）→ 保留最初那一份快照

        BodyState saved = new() { Path = GetCurrentBodyPath(creature) };
        if (GetBody(creature) is Sprite2D sprite)
        {
            saved.Scale = sprite.Scale;
            saved.Position = sprite.Position;
        }

        SavedBodyStates[key] = saved;
        FaceFormActive.Add(key);
    }

    /// <summary>
    /// 变脸结束：摘掉挂上去的怪物形象，并还原成**变脸前记下的那具身体**
    /// （默认图，或当时的黑太岁形态）。没有快照（例如中途读档）时退回默认形象。
    /// </summary>
    public static bool RestoreBodyAfterFaceForm(Creature? creature)
    {
        bool cleared = ClearMonsterVisuals(creature);

        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return cleared;

        ulong key = node.GetInstanceId();
        FaceFormActive.Remove(key);   // 变脸结束，下一次打出会重新拍快照

        if (!SavedBodyStates.Remove(key, out BodyState saved) || string.IsNullOrEmpty(saved.Path))
            return ResetBodyTexture(creature);   // 没快照 → 退回默认形象

        if (GetBody(creature) is not Sprite2D sprite)
            return cleared;

        if (ResourceLoader.Exists(saved.Path))
        {
            sprite.Texture = ResourceLoader.Load<Texture2D>(saved.Path);
        }
        else
        {
            Entry.Logger.Warn($"[lihuowang2] 变脸：还原形象时找不到贴图，退回默认：{saved.Path}");
            return ResetBodyTexture(creature);
        }

        sprite.Scale = saved.Scale;
        sprite.Position = saved.Position;
        CurrentBodyPaths[key] = saved.Path;
        return true;
    }

    /// <summary>把形象还原：贴图/缩放/位置回到改图前，并摘掉挂上去的怪物形象。</summary>
    public static bool ResetBodyTexture(Creature? creature)
    {
        bool cleared = ClearMonsterVisuals(creature);

        // 身体被强制回到默认：把"当前形象"和“变脸快照”一起改成默认，
        // 这样即使之后变脸结束，也不会把已经撤掉的黑太岁形态又还原回来。
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node)
        {
            ulong key = node.GetInstanceId();
            CurrentBodyPaths[key] = DefaultBodyPath;
            if (SavedBodyStates.TryGetValue(key, out BodyState saved))
            {
                saved.Path = DefaultBodyPath;
                SavedBodyStates[key] = saved;
            }
        }

        if (GetBody(creature) is not Sprite2D sprite)
            return cleared;

        if (OriginalStates.TryGetValue(sprite.GetInstanceId(), out BodyState state))
        {
            sprite.Texture = state.Texture;
            sprite.Scale = state.Scale;
            sprite.Position = state.Position;
            return true;
        }

        // 没记录过（例如中途读档）就退回默认贴图。
        if (!ResourceLoader.Exists(DefaultBodyPath))
        {
            Entry.Logger.Warn($"[lihuowang2] 默认形象贴图不存在：{DefaultBodyPath}");
            return cleared;
        }

        sprite.Texture = ResourceLoader.Load<Texture2D>(DefaultBodyPath);
        return true;
    }

    // 摘掉挂在某个单位上的怪物形象：释放节点、恢复原形象的可见性。
    private static bool ClearMonsterVisuals(Creature? creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            return false;

        if (!MonsterVisuals.Remove(node.GetInstanceId(), out MonsterVisualState? state))
            return false;

        if (GodotObject.IsInstanceValid(state.Visuals))
            state.Visuals.QueueFree();

        // 把原形象放出来（它一直保留着血量条/命中框所需的信息，只是刚才被藏起来了）。
        if (GodotObject.IsInstanceValid(state.Node))
            state.Node.Visuals.Visible = true;

        state.Animator = null;
        return true;
    }

    // Creature -> NCreature -> NCreatureVisuals -> 当前 body（%Visuals）。
    private static Node2D? GetBody(Creature? creature)
        => NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?.GetCurrentBody();
}
