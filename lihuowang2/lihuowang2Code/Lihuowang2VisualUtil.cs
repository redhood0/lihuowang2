using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace lihuowang2;

// 战斗人物形象（%Visuals 那张 Sprite2D）的运行时替换工具。
//
// 原理：自定义角色的战斗场景（lihuowang2_character.tscn）本身就是 NCreatureVisuals 本体 ——
// 引擎在 NCreatureVisuals._Ready() 里执行 _body = GetNode<Node2D>("%Visuals")，
// 所以只要换掉 %Visuals 的 Texture 就能立刻改变角色外观。
//
// 注意：
//  * 判定框 / 特效锚点来自场景里写死的 %Bounds、%CenterPos、%IntentPos，不会跟着贴图变，
//    所以新图最好和原图构图接近（人物脚底位置、整体大小）。需要时用 scale/position 参数微调。
//  * 每个客户端各自持有战斗场景，所以换图只影响本机显示；新战斗开始时场景重新实例化，自动恢复默认。
public static class Lihuowang2VisualUtil
{
    // 默认形象。
    public const string DefaultBodyPath = $"{Entry.ResPath}/images/characters/lhw_character.png";

    // 「黑太岁」转化形象。
    public const string HeitaisuiBodyPath = $"{Entry.ResPath}/images/characters/lhw_trans01.png";

    // 记录每个身体节点第一次被改图前的原始状态，还原时精确恢复（多人下每个角色各一份）。
    private static readonly Dictionary<ulong, BodyState> OriginalStates = [];

    private struct BodyState
    {
        public Texture2D? Texture;
        public Vector2 Scale;
        public Vector2 Position;
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

        return true;
    }

    /// <summary>把形象还原成改图前记录下来的原样（贴图、缩放、位置一起还原）。</summary>
    public static bool ResetBodyTexture(Creature? creature)
    {
        if (GetBody(creature) is not Sprite2D sprite)
            return false;

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
            return false;
        }

        sprite.Texture = ResourceLoader.Load<Texture2D>(DefaultBodyPath);
        return true;
    }

    // Creature -> NCreature -> NCreatureVisuals -> 当前 body（%Visuals）。
    private static Node2D? GetBody(Creature? creature)
        => NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?.GetCurrentBody();
}
