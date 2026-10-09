using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MinionLib.Layout;
using MinionLib.Minion;
using lihuowang2.Powers;
using lihuowang2.RitsuAdapters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace lihuowang2.Minions;

// 随从「秋吃饱」：1 点生命，站在玩家后方，不替玩家承受伤害。
// 身上挂「修真」：存活期间召唤者最大能量 -1，死亡时把这 1 点还回去。
[RegisterMonster]
public sealed class QiuChiBao : ModMinionTemplate
{
    // 注册专属摆位（比默认位置再远离玩家 20px）。
    // 优先级给 20：必须比 TestMinionLayout(10) 先执行——布局是按优先级从高到低跑、
    // 已写进 context.Positions 的节点不会再被后面的布局处理；而 TestMinionLayout 会处理
    // 所有随从，如果它先跑，就会把秋吃饱也认领掉、我们这次的偏移就轮不上了。
    // 反过来本布局只认领秋吃饱，所以曹操的偏移不受影响。
    // 时机是安全的：静态构造在首次访问本类型时执行（PlayerCmd.AddPet<T> 会先取 ModelDb.Monster<QiuChiBao>()），
    // 一定早于 MinionAnimCmd.Rearrange()。
    static QiuChiBao()
    {
        EnsureLayoutRegistered();
    }

    // 幂等注册：静态构造（最早时机）与 OnSummon（保证早于 Rearrange）各调一次，实际只会注册一次。
    private static bool _layoutRegistered;

    private static void EnsureLayoutRegistered()
    {
        if (_layoutRegistered)
            return;

        _layoutRegistered = true;
        MinionLayoutManager.Register(new QiuChiBaoLayout(), priority: 20);
    }

    // 1 点生命（Min = Max，所以召唤出来的血量固定是 1）
    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    // 血条长度修正（像素）。
    // 引擎算血条宽度时用的是「视觉边界宽度 + (24 - 本值)」
    // （见 NHealthBar.UpdateLayoutForCreatureBounds：x = bounds.Size.X + (24f - Monster.HpBarSizeReduction)），
    // 而边界取的是贴图矩形 —— qiuchibao.png 是 205px 宽、可见画面只占中间 154px，
    // 所以默认会画出一条约 229px 的长血条，对一个 1 血随从太长了。
    // 本值越大血条越短：100 → 约 129px。想再短就调大（每 +1 短 1px），
    // 调过头会挤到血条数字，建议保持在 60~130 之间。
    public override float HpBarSizeReduction => 100f;

    // 随从形象：res://lihuowang2/images/minions/qiuchibao.png
    // （Godot 导入后打进 pck；加载失败会回落到 AssetProfile / 引擎默认视觉。）
    // public：卡牌「修真·秋吃饱」的随从 hover 要拿它当图标。
    public const string SpritePath = "res://lihuowang2/images/minions/qiuchibao.png";

    public override MonsterAssetProfile AssetProfile => new(SpritePath);

    // 用一张贴图直接构造随从视觉（与 TestMinion 同一套做法）。
    // 引擎里生物的原点在"脚底"，而 Sprite2D 默认以图片中心对齐原点 →
    // 这里按图片高度上抬一半，让图片底部正好站在脚下。
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        Texture2D? texture = GD.Load<Texture2D>(SpritePath);
        if (texture == null)
        {
            return null;
        }

        const float LiftRatio = 0.5f;
        VisualNodeStyle style = VisualNodeStyle.Create(
            position: new Vector2(0f, -texture.GetHeight() * LiftRatio));

        return RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(texture, style);
    }

    // 名字（本地化表 monsters）
    public override LocString Title =>
        MonsterModel.L10NMonsterLookup("LIHUOWANG2_MONSTER_QIU_CHI_BAO.name");

    // 召唤时说的那句（同一张本地化表 monsters）。
    // 想改台词只动 localization/<lang>/monsters.json，不用碰代码。
    private const string SummonLineKey = "LIHUOWANG2_MONSTER_QIU_CHI_BAO.talk.summon";

    // 气泡配色 / 停留时长（秒）。
    private const VfxColor BubbleColor = VfxColor.Cyan;
    private const double BubbleSeconds = 2.25;

    // 气泡挂点微调（像素；X 右为正 / Y 下为正）。
    // 想让它更靠上就把 Y 调得更负，想更靠右就把 X 调大。
    private static readonly Vector2 BubbleOffset = new(0f, -6f);

    /// <summary>
    /// 在秋吃饱头顶说一句召唤台词（对话气泡）。由卡牌「修真·秋吃饱」在召唤后调用。
    ///
    /// 实现（挂点、"往哪边摊开"、防出屏）都在共用的 MinionSpeechBubble 里。
    /// 秋吃饱站在玩家**后方**（左手边），所以气泡要**向左**摊开（extendRight: false）——
    /// 往右摊开会正好盖在主角头上、看着像主角在说话。
    /// </summary>
    public static void PlaySummonLine(Creature minion)
        => MinionSpeechBubble.Play(minion, "monsters", SummonLineKey,
            BubbleColor, BubbleSeconds, BubbleOffset, extendRight: false);

    // 召唤时挂上「修真」与「石渎」。
    // 故意不挂 MinionGuardianPower：那是 TestMinion 用来替玩家承伤的「守护」，
    // 本随从要求「无法承担伤害」，所以不挂 —— 它就不会把打向玩家的未格挡伤害转移到自己身上。
    // （站位 Back 也不满足守护的启用条件，属于双保险。）
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner,
        MinionSummonOptions options)
    {
        // 摆位注册兜底（幂等）：MinionCmd.AddMinion 的顺序是
        // 「PlayerCmd.AddPet<T> → OnSummon → MinionAnimCmd.Rearrange()」，
        // 在这里注册一定早于本次 Rearrange，摆位必定生效（静态构造不保证跑到）。
        EnsureLayoutRegistered();

        // 修真：存活期间召唤者最大能量 -层数，随从死亡时随能力一起归还
        await PowerCmd.Apply<XiuZhenPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 石渎：召唤者回合结束时获得 1 个「药水形状的石头」
        await PowerCmd.Apply<ShiDuPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 召唤台词**不在这里**播：此刻摆位补间还没开始（AddMinion 是「AddPet → OnSummon → Rearrange」），
        // 随从节点还贴在主角身上，而气泡坐标只算一次、之后不跟随 → 会看起来像主角在说话。
        // 交给卡牌「修真·秋吃饱」在 AddMinion 之后再等 0.3 秒调 PlaySummonLine(...)。
    }
}
