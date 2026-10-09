using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MinionLib.Layout;
using MinionLib.Minion;
using lihuowang2.Powers;
using lihuowang2.RitsuAdapters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace lihuowang2.Minions;

// 随从「和尚」：6 点生命，站在玩家**后方**，不替玩家承受伤害。
// 身上挂四个能力：
//   · 先天一炁 1 层：存活期间召唤者最大能量 -1，死亡时随能力一起失效（= 归还那 1 点）；
//   · 唯一（UniquePower）：它是唯一单位 —— 已经在场时再召唤，不生成第二只，改为给已有那只加血
//     （机制见 ReinforceIfExisting，官方 Osty 同款）；
//   · 诵经（SongJingPower）：积攒到 10 层（召唤时自带 1 层）。召唤者的回合结束时 +1，或已有和尚时再打出召唤牌 +1；
//     每 +1 都随机念一句；攒到 10 层就改说「噫，不曾想我竟是世尊~」、放个官方特效并对随机敌人砸 999 点，
//     然后层数回到 1 层重新攒（台词在本类，结算在能力里）；
//   · 行善（XingShanPower）：召唤者的回合开始时，随机移除一个**友方角色**（玩家，含双人模式的队友）身上的一层
//     负面状态、在那个角色身上播官方特效，然后和尚自己失去 1 点生命（所以那 6 点血会被慢慢耗掉）。
//
// 与「彭龙腾」的区别：不挂「守护」（MinionGuardianPower）、不挂「将相首」，
// 站位在后方、且比「秋吃饱」更贴主角一点（见 HeShangLayout.CloserDistance）。
[RegisterMonster]
public sealed class HeShang : ModMinionTemplate
{
    // 注册专属摆位（后方 + 比默认位置再靠近玩家 20px，见 HeShangLayout）。
    // 优先级给 20（与秋吃饱 / 彭龙腾一致）：布局按优先级从高到低跑，已写进 context.Positions 的节点
    // 不会再被后面的布局处理，而 TestMinionLayout 会认领"所有随从"，所以必须比它(10)先跑。
    // 本布局只认领和尚，所以不会影响秋吃饱 / 彭龙腾的偏移。
    // ⚠ 注册时机：静态构造只是"最早"的时机，**不保证**在 MinionAnimCmd.Rearrange() 之前跑过，
    // 只靠它会出现"摆位静默不生效"，所以 OnSummon 里还会再兜一次（见 EnsureLayoutRegistered）。
    static HeShang()
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
        MinionLayoutManager.Register(new HeShangLayout(), priority: 20);
    }

    // 召唤血量（兜底）：正常召唤都走「修真·和尚」，血量由那张牌的 MinionHp 参数传进来
    // （MinionSummonOptions.MaxHp，真正落地见 OnSummon）；只有不走那张牌的召唤（脚本/测试/以后新增的来源）
    // 才会用到这个数。改血量请改召唤牌的 MinionHp —— 它会连带改掉卡面文字与重复召唤的加血量。
    private const int DefaultSummonHp = 6;

    public override int MinInitialHp => DefaultSummonHp;

    public override int MaxInitialHp => DefaultSummonHp;

    // 唯一单位：如果该玩家名下已经有一只「活着的」和尚，就不再召唤第二只，
    // 而是给已有的那一只「获得对应的血量」= +amount 最大生命并回复等量生命。
    // amount 由召唤方传进来（「修真·和尚」传的是它自己的 MinionHp 参数）。
    //
    // 与官方「奥斯提」完全同款（OstyCmd.Summon 的文档注释原文：
    //   "If the specified creature already owns an instance of Osty, raise Osty's max HP by the specified number instead."
    // 它的实现就是 CreatureCmd.GainMaxHp(现有的奥斯提, 本次召唤的血量)；
    // 而 GainMaxHp 内部 = SetMaxHp(最大生命 + N) + Heal(N)，所以最大生命与当前生命一起涨）。
    //
    // 返回 true = 已经强化了原有单位，调用方不要再召唤（见 lihuowang2XiuZhenHeShang.OnPlay）。
    // 说明：随从死亡后引擎会把它移出 Pets 列表（PlayerCombatState.OnPetDied），
    // 所以死亡后再召唤会正常新建一只；这里仍显式判 IsAlive 以防"尸体还没被清掉"的那一帧。
    public static async Task<bool> ReinforceIfExisting(Player owner, decimal amount)
    {
        Creature? existing = owner.PlayerCombatState?.GetPet<HeShang>();
        if (existing == null || !existing.IsAlive)
        {
            return false;
        }

        await CreatureCmd.GainMaxHp(existing, amount);
        return true;
    }

    // ===== 血条宽度 =====
    // 引擎公式（NHealthBar.UpdateLayoutForCreatureBounds）：
    //     血条宽 = hitbox 宽 + (24 − HpBarSizeReduction)
    //   而 RitsuLib 的贴图生物工厂把 hitbox 宽设成「贴图尺寸 × 1.1」（_ritsulib_all.cs:162596），
    // 所以反推：本值 = 贴图宽 × 1.1 + 24 − 目标宽。
    // 参考：「彭龙腾」的血条也是 180px 宽（它是先收窄 %Bounds、再给固定 24 做到的同样结果）；
    // 和尚的贴图本来就窄（189px），所以直接用公式算，不必再改 %Bounds。
    private const float TargetHpBarWidth = 180f;

    private const float BoundsWidthFactor = 1.1f;

    // 贴图像素宽（缓存；加载失败记 0 → 本值退化成 24，血条回到工厂默认宽度，不会出现负宽度）
    private static float _spritePixelWidth = -1f;

    private static float SpritePixelWidth
    {
        get
        {
            if (_spritePixelWidth < 0f)
                _spritePixelWidth = GD.Load<Texture2D>(SpritePath)?.GetWidth() ?? 0f;

            return _spritePixelWidth;
        }
    }

    public override float HpBarSizeReduction =>
        Math.Max(0f, SpritePixelWidth * BoundsWidthFactor + 24f - TargetHpBarWidth);

    // 随从形象：res://lihuowang2/images/minions/heshang.png
    // （Godot 导入后打进 pck；加载失败会回落到 AssetProfile / 引擎默认视觉。）
    // public：卡牌「修真·和尚」的随从 hover 要拿它当图标。
    public const string SpritePath = "res://lihuowang2/images/minions/heshang.png";

    public override MonsterAssetProfile AssetProfile => new(SpritePath);

    // 用一张贴图直接构造随从视觉（与彭龙腾 / 秋吃饱同一套做法）。
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
        MonsterModel.L10NMonsterLookup("LIHUOWANG2_MONSTER_HE_SHANG.name");

    // ===== 诵经台词（能力「诵经」调用：倒计时没到点随机念，到点改说「世尊」）=====
    // 台词写在本地化表 monsters 里，想改词只动 localization/<lang>/monsters.json。
    private const string ChantLineKey0 = "LIHUOWANG2_MONSTER_HE_SHANG.talk.chant.0";
    private const string ChantLineKey1 = "LIHUOWANG2_MONSTER_HE_SHANG.talk.chant.1";
    private const string FinaleLineKey = "LIHUOWANG2_MONSTER_HE_SHANG.talk.chant.final";

    // 随机念的那两句（随机取一条）
    private static readonly string[] ChantLineKeys = [ChantLineKey0, ChantLineKey1];

    // 气泡配色 / 停留时长（秒）/ 挂点微调。配色给 Golden（诵经感），想统一成秋吃饱那种青色就改 Cyan。
    private const VfxColor BubbleColor = VfxColor.Gold;
    private const double BubbleSeconds = 2.25;
    private static readonly Vector2 BubbleOffset = new(0f, -6f);

    /// <summary>
    /// 还没攒满：随机念一句（善哉善哉 / 阿弥陀佛）。由能力「诵经」在每次 +1 层时调用
    ///（召唤者的回合结束，或已有和尚时再打出召唤牌）。
    /// 随机走引擎的确定性随机流（CombatTargets），联机两边念的是同一句。
    /// </summary>
    public static void PlayChantLine(Creature minion, Player summoner)
    {
        string? key = summoner.RunState.Rng.CombatTargets.NextItem(ChantLineKeys);
        PlayLine(minion, key ?? ChantLineKey0);
    }

    /// <summary>
    /// 攒满（10 层）：说「噫，不曾想我竟是世尊~」。说完之后由能力结算那 999 点伤害。
    /// </summary>
    public static void PlayFinaleLine(Creature minion)
        => PlayLine(minion, FinaleLineKey);

    // 和尚站在玩家**后方**（Back），所以气泡要向**左**摊开（extendRight: false）——
    // 往右摊开会正好盖在主角头上、看着像主角在说话（与秋吃饱同一个坑，详见 MinionSpeechBubble）。
    private static void PlayLine(Creature minion, string locKey)
        => MinionSpeechBubble.Play(minion, "monsters", locKey,
            BubbleColor, BubbleSeconds, BubbleOffset, extendRight: false);

    // 召唤时挂「先天一炁」「唯一」「诵经」。
    // 故意不挂 MinionGuardianPower：本随从不替玩家承伤（要承伤请参考 PengLongTeng）。
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner,
        MinionSummonOptions options)
    {
        // 摆位注册兜底（幂等）：MinionCmd.AddMinion 的顺序是
        // 「PlayerCmd.AddPet<T> → OnSummon → MinionAnimCmd.Rearrange()」，
        // 所以在这里注册一定早于本次 Rearrange，摆位必定生效（静态构造不保证跑到）。
        // 注意：站位本身（后方 Back）由召唤牌「修真·和尚」指定，这里注册的只是
        // "比默认位置再靠近玩家 20px"那点偏移（见 HeShangLayout）。
        EnsureLayoutRegistered();

        // 血量参数：召唤牌通过 MinionSummonOptions.MaxHp 传进来（卡面上的「（N生命值）」就是这个数）。
        // ⚠ MinionLib 的 AddMinion 只是把这个字段透传给 OnSummon，**不会自动应用**，所以要在这里落地。
        // 用 SetMaxHp 取"实际变化量"再按变化量回血 —— 与官方 OstyCmd 用的 CreatureCmd.GainMaxHp 同一套语义
        // （GainMaxHp 内部就是 SetMaxHp + Heal）；参数与初始血量相同时变化量为 0，什么都不会发生。
        if (options.MaxHp is decimal maxHp)
        {
            decimal gained = await CreatureCmd.SetMaxHp(Creature, maxHp);
            if (gained > 0m)
            {
                await CreatureCmd.Heal(Creature, gained);
            }
        }

        // 先天一炁 1 层：存活期间召唤者最大能量 -1，随从死亡时随能力一起失效（= 归还 1 点）。
        await PowerCmd.Apply<XiuZhenPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 唯一标记：只用来显示/说明"这只单位是唯一的"（判定在 ReinforceIfExisting）。
        await PowerCmd.Apply<UniquePower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 诵经：召唤时自带 InitialStacks（1）层，之后靠召唤者回合结束 / 再用召唤牌往上攒；
        // 攒到 MaxStacks（10）层就说「世尊」、放特效、砸 999 点，然后回到 1 层重来（结算全在 SongJingPower）。
        // ⚠ 不能传 0：0 层的 Counter 能力会被引擎立刻移除（PowerModel.ShouldRemoveDueToAmount 是非虚方法），
        // 而且 PowerCmd.Apply 传 0 层本身就是空操作 —— 那样召唤完会一个图标都没有。
        await PowerCmd.Apply<SongJingPower>(choiceContext, Creature, SongJingPower.InitialStacks,
            owner.Creature, options.Source);

        // 行善：召唤者的回合开始时，随机移除一个友方（含双人模式的队友与双方随从）身上的一层负面状态。
        // 不吃层数，1 层即可。
        await PowerCmd.Apply<XingShanPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);
    }
}
