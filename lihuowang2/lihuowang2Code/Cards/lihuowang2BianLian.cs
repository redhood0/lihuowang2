using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 变脸：2 费**罕见**能力牌（**多人限定**）—— 随机变成十种怪物形象之一
// （异蛙寄生虫 / 幽灵骑士 / 劫掠者刺客 / 钙化邪教徒 / 地精佣兵 / 拳击构装体 /
//   蜂群术士 / 虔诚雕刻师 / 电球头 / 魔法骑士），
// 并获得「变脸」能力：不会成为攻击目标（怪物攻击对你无效），持续 1 回合（升级 2 回合）。
// 「变脸」是倒计时 buff（层数 = 剩余回合）：每个自己的回合开始 -1，归零即变回默认形象；
// 自己主动打出攻击牌也会立刻变回（自动打出不算）。见 Powers/BianLianPower.cs。
//
// 形象换法复用「黑太岁」那套（Lihuowang2VisualUtil）：只改本机战斗场景里 %Visuals 的贴图，
// 只影响本机显示；能力被移除（见 BianLianPower.AfterRemoved）或进入新战斗时会自动恢复。
//
// ⚠ 卡图：images/cards/lihuowang2BianLian.png（还没有这张图，现在会回落占位图）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BianLian : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 多人限定卡：引擎按 RunState.CardMultiplayerConstraint 过滤卡池 ——
    // CardPoolModel.GetUnlockedCards 会把 MultiplayerOnly 的牌从**单人**的卡牌奖励/生成里剔除
    // （单人：RemoveAll(MultiplayerOnly)；多人：RemoveAll(SingleplayerOnly)），所以：
    //   · 单人模式：不会从奖励、商店、事件里开出来（图鉴里仍能看到，方便预览）；
    //   · 多人模式：照常出现。
    // 已经拿在手里的这张牌不受影响，两种模式都能正常打出。
    // public override CardMultiplayerConstraint MultiplayerConstraint =>
    //     CardMultiplayerConstraint.MultiplayerOnly;

    // 变脸持续回合数走 DynamicVar（Turns）：基础 1 回合、升级 +1 → 2 回合。
    // 卡面用 {Turns:diff()} 引用（升级预览会显示「1→2」），施加给能力的层数也直接取它，三者同源。
    // 想改基础值/增量就改下面两处（CanonicalVars 的 1m 与 OnUpgrade 的 UpgradeValueBy(1m)）。

    // 十种形象：怪物（用它的 Spine 形象，会动）+ 可选的模组自绘贴图文件名。
    // 想换成自绘形象：把图按 LocalStem 命名丢进 lihuowang2/images/characters/ 即可，代码不用改
    // （有自绘图就优先用自绘图，否则挂怪物本体的 Spine 形象）。
    // Scale / Offset 是"个别怪物体型或站位不合适"时的微调旋钮，默认 null = 不干预（跟引擎装配时一致）。
    private sealed record Form(string LocalStem, Func<MonsterModel> Resolve,
        float? Scale = null, Vector2? Offset = null);

    private static readonly Form[] Forms =
    [
        new("bianlian_phrog_parasite",       () => ModelDb.Monster<PhrogParasite>()),
        new("bianlian_spectral_knight",      () => ModelDb.Monster<SpectralKnight>()),
        new("bianlian_assassin_ruby_raider", () => ModelDb.Monster<AssassinRubyRaider>()),
        new("bianlian_calcified_cultist",    () => ModelDb.Monster<CalcifiedCultist>()),
        new("bianlian_gremlin_merc",         () => ModelDb.Monster<GremlinMerc>()),
        new("bianlian_punch_construct",      () => ModelDb.Monster<PunchConstruct>()),
        new("bianlian_entomancer",           () => ModelDb.Monster<Entomancer>()),
        new("bianlian_devoted_sculptor",     () => ModelDb.Monster<DevotedSculptor>()),
        new("bianlian_globe_head",           () => ModelDb.Monster<GlobeHead>()),
        new("bianlian_magi_knight",          () => ModelDb.Monster<MagiKnight>())
    ];

    // 卡牌基础数值：Turns = 变脸持续回合数（基础 1 回合，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Turns", 1m)];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 升级：持续 +1 回合（1 → 2）
    protected override void OnUpgrade()
    {
        DynamicVars["Turns"].UpgradeValueBy(1m);
    }

    // 悬停提示：描述里出现的「变脸」能力
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<BianLianPower>()];

    public lihuowang2BianLian() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 随机挑一种形象。走引擎的确定性随机流（Niche，官方留给"一次性杂项随机"的流），
        //    联机时两端会挑到同一种。
        Form form = Forms[player.RunState.Rng.Niche.NextInt(Forms.Length)];

        // 2. 换形象（找不到贴图就保持原形象，只写一条警告，不阻断下面的效果）
        ApplyBodyForm(Owner.Creature, form);

        // 3. 获得「变脸」能力（不会成为攻击目标）—— 施加的层数 = 伪装剩余回合数。
        //    Counter 能力：重复打出会叠加层数（= 延长伪装），同时刷新形象。
        await PowerCmd.Apply<BianLianPower>(choiceContext, Owner.Creature,
            DynamicVars["Turns"].BaseValue, Owner.Creature, this);
    }

    // 换形象：
    //   ① 模组自绘（把 res://lihuowang2/images/characters/<LocalStem>.png 放进去就会用这张）
    //   ② 否则挂游戏本体的怪物 Spine 形象（会动，也不需要额外素材）
    //   两条都失败就保持原形象并记一条警告 —— 效果（不受怪物伤害）照常生效。
    private static void ApplyBodyForm(Creature creature, Form form)
    {
        // 0. 先给"变脸前的身体"拍一张快照：变脸结束时（能力被移除）按它还原 ——
        //    之前是默认图就回默认图，之前正处在黑太岁形态就回黑太岁形态。
        //    （重复打出变脸不会覆盖最初那一份，见 CaptureBodyForFaceForm。）
        Lihuowang2VisualUtil.CaptureBodyForFaceForm(creature);

        string localPath = $"{Entry.ResPath}/images/characters/{form.LocalStem}.png";
        if (ResourceLoader.Exists(localPath))
        {
            // 自绘图只是变脸的"临时外观"，不算身体本身换了形态：
            // 先记下当前身体形象，换完之后登记回去，免得变脸结束时把这张怪物图当成"变脸前的形象"。
            string bodyPath = Lihuowang2VisualUtil.GetCurrentBodyPath(creature);
            Lihuowang2VisualUtil.SetBodyTexture(creature, localPath);
            Lihuowang2VisualUtil.SetCurrentBodyPath(creature, bodyPath);
            return;
        }

        MonsterModel monster = form.Resolve();
        if (!Lihuowang2VisualUtil.SetBodyMonsterVisuals(creature, monster, form.Scale, form.Offset))
            Entry.Logger.Warn($"[lihuowang2] 变脸：挂载 {monster.Id.Entry} 的怪物形象失败，本次保持原形象。");
    }
}
