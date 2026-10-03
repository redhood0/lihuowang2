using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MinionLib.Layout;
using MinionLib.Minion;
using MinionLib.Powers;
using lihuowang2.Powers;
using lihuowang2.RitsuAdapters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace lihuowang2.Minions;

// 随从「彭龙腾」：10 点生命，站在玩家前方，替玩家承受未格挡的攻击伤害（守护）。
// 身上挂「先天一炁」1 层：存活期间召唤者最大能量 -1，死亡时随能力一起归还。
// 血条会像奥斯提那样显示「共享格挡」：打向它的伤害由主人的格挡池吸收，
// 所以血条跟踪主人的格挡状态（见 OnSummon 末尾的 TrackBlockStatus）。
//
// 另外挂两个能力：
//   · 唯一（UniquePower）：它是唯一单位 —— 已经在场时再召唤，不生成第二只，改为给已有那只加血
//     （机制见 ReinforceIfExisting，官方 Osty 同款）；
//   · 将相首（JiangXiangShouPower）：召唤者的回合开始时，随机给 1 张战士牌并本回合 0 费。
[RegisterMonster]
public sealed class PengLongTeng : ModMinionTemplate
{
    // 注册专属摆位（比默认位置再远离玩家一点，见 PengLongTengLayout.ExtraDistance）。
    // 优先级给 20（与秋吃饱一致）：布局按优先级从高到低跑，已写进 context.Positions 的节点
    // 不会再被后面的布局处理，而 TestMinionLayout 会认领"所有随从"，所以必须比它(10)先跑。
    // 本布局只认领彭龙腾，所以不会影响秋吃饱/曹操的偏移。
    // ⚠ 注册时机：静态构造只是"最早"的时机，**不保证**在 MinionAnimCmd.Rearrange() 之前跑过，
    // 只靠它会出现"摆位静默不生效"，所以 OnSummon 里还会再兜一次（见 EnsureLayoutRegistered）。
    static PengLongTeng()
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
        MinionLayoutManager.Register(new PengLongTengLayout(), priority: 20);
    }

    // 默认召唤血量（兜底）：正常召唤都走「修真·彭龙腾」，血量由那张牌的 MinionHp 参数传进来
    // （MinionSummonOptions.MaxHp，真正落地见 OnSummon）；只有不走那张牌的召唤（脚本/测试/以后新增的来源）
    // 才会用到这个数。改血量请改召唤牌的 MinionHp —— 它会连带改掉卡面文字与重复召唤的加血量。
    private const int DefaultSummonHp = 20;

    public override int MinInitialHp => DefaultSummonHp;

    public override int MaxInitialHp => DefaultSummonHp;

    // 唯一单位：如果该玩家名下已经有一只「活着的」彭龙腾，就不再召唤第二只，
    // 而是给已有的那一只「获得对应的血量」= +amount 最大生命并回复等量生命。
    // amount 由召唤方传进来（「修真·彭龙腾」传的是它自己的 MinionHp 参数）。
    //
    // 与官方「奥斯提」完全同款（OstyCmd.Summon 的文档注释原文：
    //   "If the specified creature already owns an instance of Osty, raise Osty's max HP by the specified number instead."
    // 它的实现就是 CreatureCmd.GainMaxHp(现有的奥斯提, 本次召唤的血量)；
    // 而 GainMaxHp 内部 = SetMaxHp(最大生命 + N) + Heal(N)，所以最大生命与当前生命一起涨）。
    //
    // 返回 true = 已经强化了原有单位，调用方不要再召唤（见 lihuowang2XiuZhenPengLongTeng.OnPlay）。
    // 说明：随从死亡后引擎会把它移出 Pets 列表（PlayerCombatState.OnPetDied），
    // 所以死亡后再召唤会正常新建一只；这里仍显式判 IsAlive 以防"尸体还没被清掉"的那一帧。
    public static async Task<bool> ReinforceIfExisting(Player owner, decimal amount)
    {
        Creature? existing = owner.PlayerCombatState?.GetPet<PengLongTeng>();
        if (existing == null || !existing.IsAlive)
        {
            return false;
        }

        await CreatureCmd.GainMaxHp(existing, amount);
        return true;
    }

    // ===== 可调参数 1：血条最终宽度（像素）=====
    // 只改这一个数字就能调血条长度（hitbox / power 图标行会一起跟着变，见 StretchUiBounds）。
    // 引擎公式（NHealthBar.UpdateLayoutForCreatureBounds）：
    //     血条宽 = hitbox 宽 + (24 − HpBarSizeReduction)
    //   收窄后 hitbox 宽 = 本值，所以给 24 即"血条 = 本值"。
    // 参考：140 ≈ 秋吃饱和 167px 贴图那种"短血条"的观感。
    private const float TargetHpBarWidth = 180f;

    // 收窄失败时的兜底系数：RitsuLib 的贴图生物工厂把 %Bounds 设成「贴图尺寸 × 1.1」
    // （_ritsulib_all.cs:162596），所以没收到窄时 hitbox 宽 = 贴图宽 × 1.1（不是贴图宽本身）。
    private const float BoundsWidthFactor = 1.1f;

    // %Bounds 是否成功收窄（见 StretchUiBounds）。
    // 收窄成功 → hitbox / 血条 / power 图标行全部由 TargetHpBarWidth 决定（同一个数据源）。
    private static bool _uiBoundsNarrowed;

    // 微调：收窄后把矩形整体左右平移这么多像素（正数 = 右移）。
    // 默认 0 在数学上就是"正好居中"（%Bounds 由 RitsuLib 工厂以贴图中心为原点生成）；
    // 只有当立绘左右透明边距不对称、实测仍差几像素时才需要动它。
    private const float UiCenterNudge = 0f;

    // 贴图像素宽（缓存；加载失败记 0 → 不做缩减，回落到默认血条，不会出现负宽度）
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

    // 血条宽 = hitbox 宽 + (24 − 本值)（NHealthBar.UpdateLayoutForCreatureBounds：
    //   left = hitbox.left − 本值/2、width = hitbox.width + 本值 ⇒ 血条以 hitbox 为中心）。
    //   · %Bounds 已收窄（StretchUiBounds）→ hitbox 宽 = TargetHpBarWidth，给 24 使血条正好 = 目标宽；
    //     而 power 图标行的宽度 = hitbox 宽 + 25，是同一个 hitbox 推出来的，所以两者自动对齐。
    //   · 收窄失败（取不到 %Bounds）→ 用工厂系数反推贴图对应的 hitbox 宽，兜底值仍是目标宽。
    public override float HpBarSizeReduction =>
        _uiBoundsNarrowed
            ? 24f
            : Math.Max(0f, SpritePixelWidth * BoundsWidthFactor + 24f - TargetHpBarWidth);

    // 随从形象：res://lihuowang2/images/minions/penglongteng.png
    // （Godot 导入后打进 pck；加载失败会回落到 AssetProfile / 引擎默认视觉。）
    // public：卡牌「修真·彭龙腾」的随从 hover 要拿它当图标。
    public const string SpritePath = "res://lihuowang2/images/minions/penglongteng.png";

    public override MonsterAssetProfile AssetProfile => new(SpritePath);

    // 用一张贴图直接构造随从视觉（与 TestMinion / 秋吃饱同一套做法）。
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

        NCreatureVisuals visuals =
            RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(texture, style);

        StretchUiBounds(visuals);

        return visuals;
    }

    // 把 visuals 的 %Bounds 收窄到 TargetHpBarWidth。
    // hitbox / 血条 / power 图标行 / 名字条 全部从这一个矩形推导（NCreature._Ready → UpdateBounds →
    // _stateDisplay.SetCreatureBounds），所以收窄它 = 四者一起变窄并保持互相对齐。
    //
    // ⚠ 收窄时必须同时把矩形「中心」补回来，否则 hitbox 会整体偏左：
    //   · RitsuLib 的贴图生物工厂（_ritsulib_all.cs:162596）给的是
    //         Size = 贴图尺寸 × 1.1f、Position = (−宽/2, −高)   ← 左边缘是 −宽/2，矩形以 x=0 居中；
    //   · 引擎只把 %Bounds 的「左边缘 + 尺寸」当 hitbox（NCreature.UpdateBounds：
    //         Hitbox.GlobalPosition = 节点位置 + (%Bounds.GlobalPosition − 节点位置)、Hitbox.Size = %Bounds.Size）。
    //   所以只改 Size、不动 Position = "固定左边缘往右缩" → 矩形中心左移(原宽−新宽)/2。
    //   这里把左边缘右移同样的半个差值，让中心保持 x=0，hitbox 才会继续和立绘居中。
    // 时机安全：本方法在建节点时执行，早于 NCreature._Ready 里那次 UpdateBounds(Visuals)。
    private static void StretchUiBounds(NCreatureVisuals visuals)
    {
        Control? uiBounds = visuals.GetNodeOrNull<Control>("%Bounds");
        if (uiBounds == null)
        {
            return;
        }

        float originalWidth = uiBounds.Size.X;
        float narrowedWidth = Math.Min(TargetHpBarWidth, originalWidth);
        if (narrowedWidth >= originalWidth)
        {
            return;   // 立绘本来就比目标窄：保持原样（只缩不放）
        }

        uiBounds.Size = new Vector2(narrowedWidth, uiBounds.Size.Y);
        uiBounds.Position += new Vector2((originalWidth - narrowedWidth) * 0.5f + UiCenterNudge, 0f);

        _uiBoundsNarrowed = true;
    }

    // 名字（本地化表 monsters）
    public override LocString Title =>
        MonsterModel.L10NMonsterLookup("LIHUOWANG2_MONSTER_PENG_LONG_TENG.name");

    // 召唤时挂「守护」「先天一炁」「唯一」「将相首」。
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner,
        MinionSummonOptions options)
    {
        // 摆位注册兜底（幂等）：静态构造是最早时机，但不保证一定在 Rearrange 之前跑过。
        // MinionCmd.AddMinion 的顺序是「PlayerCmd.AddPet<T> → OnSummon → MinionAnimCmd.Rearrange()」，
        // 所以在这里注册一定早于本次 Rearrange，摆位必定生效。
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

        // 守护：把本该打在宠物主人（玩家）身上的未格挡伤害转移到本随从身上。
        // MinionGuardianPower 内部的前置条件是「随从必须站在 MinionPosition.Front」，
        // 召唤牌已经指定了 Front（见 lihuowang2XiuZhenPengLongTeng.OnPlay）。
        await PowerCmd.Apply<MinionGuardianPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 先天一炁 1 层：存活期间召唤者最大能量 -1，随从死亡时随能力一起失效（= 归还 1 点）。
        await PowerCmd.Apply<XiuZhenPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 唯一标记：只用来显示/说明"这只单位是唯一的"（判定在 ReinforceIfExisting）。
        await PowerCmd.Apply<UniquePower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 将相首：召唤者的回合开始时，随机给 1 张战士牌并本回合 0 费。
        await PowerCmd.Apply<JiangXiangShouPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);

        // 与奥斯提同款：把「召唤者的格挡」显示到本随从的血条上。
        //
        // 为什么格挡是共享的：CreatureCmd.Damage 里扣格挡用的是
        //   Creature blockOwner = originalTarget2.PetOwner?.Creature ?? originalTarget2;
        //   blockedDamage = blockOwner.DamageBlockInternal(modifiedAmount, props);
        // 也就是打向宠物的伤害由「宠物主人的格挡池」吸收 —— 所以血条也该把它显示出来。
        // 引擎对 Osty 就是这么做的（OstyCmd.Summon → ostyNode?.TrackBlockStatus(summoner2.Creature)），
        // 而 NCreature.TrackBlockStatus 的 XML 注释写明了用途：「让宠物在主人有格挡时显示额外 UI」。
        // 纯 UI 跟踪（只订阅 BlockChanged 刷新血条数值），不写任何同步状态，联机安全。
        // 节点在这里一定存在：MinionCmd.AddMinion 内部先 await PlayerCmd.AddPet<T>（会建节点）
        // 再回调 OnSummon；失败时 ?. 会静默跳过。
        NCombatRoom.Instance?.GetCreatureNode(Creature)?.TrackBlockStatus(owner.Creature);
    }
}
