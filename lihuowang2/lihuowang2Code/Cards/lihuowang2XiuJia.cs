using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 修假：1 费稀有技能 —— 给随机 1 张手牌附魔（附魔效果本身也是随机的）。
//
// 三条**不写进卡面**的设定：
//   1. 附魔效果随机：从 EnchantmentPool 里挑一个"官方 CanEnchant 说能附到那张牌上"的
//      （攻击牌/技能牌的专属附魔不会被乱附：判定用官方规则，不自己维护类型表）；
//   2. 战斗结束后附魔会消失：本牌只给**战斗内的卡牌实例**附魔，牌组里的本体不受影响
//      （战斗手牌是从牌组克隆出来的，战斗一结束这层附魔自然就没了，不需要额外的清理钩子）；
//   3. 手里没有可附魔的牌时什么都不发生（不会空转、也不消耗别的资源）；
//   4. 目标牌身上若已经有"战斗内临时附魔"，会**先把它清掉，再随机一个新附魔**（永久附魔不动，见 HasTemporaryEnchantment）。
//
// 升级：从"随机 1 张"变成"随机 2 张"（每张各随机一个附魔）。
//
// 选牌潜规则（不写进卡面，见 PickTargets）：
//   · 永久附魔的牌**一律不碰** —— 不清、不覆盖、也不占名额（那是牌组里攒下的东西，不该被这张战斗内的一次性牌动到）；
//   · **优先挑身上一点附魔都没有的牌**；名额没凑够时，才退用"只有战斗内临时附魔"的牌（先清掉旧的再随机新的）。
//
// ⚠ 安全过滤（见 IsRandomSafe）：官方附魔里有两个"只在特定场景成立"的，随机附到战斗手牌上会空引用崩游戏。
//   Inky 按组合屏蔽（只允许附到有敌方目标的牌上）；Goopy 已直接移出池子，守卫保留作双保险。
// ⚠ 体验过滤（同样在 IsRandomSafe）：PerfectFit（完美契合 = 洗牌时把这张牌放到抽牌堆顶）
//   不会随机到【消耗】牌与能力牌上 —— 这两类打完就离开牌组、永远不会被洗回抽牌堆，附了等于空附魔。
//
// ⚠ 卡图：images/cards/lihuowang2XiuJia.png（还没有这张图，现在会回落占位图）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XiuJia : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 附魔强度：官方附魔都用 Amount 表示强度
    // （Adroit 给的格挡、Sharp/Vigorous 给的伤害、Nimble 给的格挡、Sown 给的能量、Swift 抽的牌数……）。
    // 当前 2 —— 吃 Amount 的这批翻倍变强；不吃 Amount 的那批（Corrupted/Instinct 的倍率、
    // Glam/Spiral 的额外结算次数、Steady/Slither 等）不受影响。
    // 想让随机附魔更强/更弱就改这一个数。
    private const decimal EnchantAmount = 2m;

    // 本牌一次附魔几张：基础 1 张，升级 2 张。
    private const int BaseEnchantCount = 2;
    private const int UpgradedEnchantCount = 3;

    // 可随机的官方附魔池（都在 MegaCrit.Sts2.Core.Models.Enchantments）。
    // 只收"有实际效果"的，故意排除：
    //   · Clone（克隆）：本身不做事，只被营地"复制卡牌"选项引用；
    //   · DeprecatedEnchantment：废弃占位；
    //   · Mocks 里的测试附魔（MockFreeEnchantment 等）；
    //   · Imbued（注能）：按需求排除 —— 它的效果是"第 1 回合开始自动打出这张牌"，随机附到手牌上体验很怪；
    //   · Goopy（黏糊）：按需求排除 —— 除了体验怪异，它还有个崩点（见 IsRandomSafe ②）；
    //   · RoyallyApproved（王室认证）：按需求排除（给牌加【固有】【保留】）；
    //   · TezcatarasEmber（特兹卡塔拉的余烬）：按需求排除（把费用降到 0 并加【永恒】）。
    // PerfectFit（完美契合）保留在池子里，但会**按牌过滤**（消耗牌/能力牌上不随机出它，见 IsRandomSafe ③）。
    // 想加减候选就改这个数组。
    private static readonly Type[] EnchantmentPool =
    [
        typeof(Adroit), typeof(Corrupted), typeof(Glam),
        typeof(Inky), typeof(Instinct), typeof(Momentum), typeof(Nimble), typeof(PerfectFit),
        typeof(Sharp), typeof(Slither), typeof(SlumberingEssence),
        typeof(SoulsPower), typeof(Sown), typeof(Spiral), typeof(Steady), typeof(Swift),
        typeof(Vigorous)
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2XiuJia() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return Task.CompletedTask;

        // 手牌：本牌打出后已经不在手里，所以这里拿到的就是"其它手牌"
        List<CardModel> hand = PileType.Hand.GetPile(player).Cards.ToList();
        if (hand.Count == 0)
            return Task.CompletedTask;   // 没有手牌：什么都不发生

        // 基础 1 张 / 升级 2 张；选谁见 PickTargets
        List<CardModel> targets = PickTargets(
            player, hand, IsUpgraded ? UpgradedEnchantCount : BaseEnchantCount);

        List<CardModel> enchanted = [];
        foreach (CardModel card in targets)
        {
            if (EnchantRandomly(player, card))
                enchanted.Add(card);
        }

        // 弹预览让玩家看到附了什么（没有附上任何东西时不弹）
        if (enchanted.Count > 0)
            CardCmd.Preview(enchanted);

        return Task.CompletedTask;
    }

    // 选这次的附魔目标（不写进卡面）：
    //   ① 永久附魔的牌**直接排除** —— 不清、不覆盖、也不占名额；
    //   ② 首选"身上一点附魔都没有"的牌；
    //   ③ 名额没凑够时，才退用"只有战斗内临时附魔"的牌（EnchantRandomly 会先清掉旧的再随机新的）；
    //   ④ 随机走引擎的确定性随机流 Niche（两端一致，与官方"迅捷"遗物挑附魔牌同一套）。
    // 先筛后随机的意义：保证不会随机到一张"附什么都附不上"的牌导致这张卡空转。
    private static List<CardModel> PickTargets(Player player, List<CardModel> hand, int count)
    {
        if (count <= 0)
            return [];

        List<CardModel> untouched = [];   // 一点附魔都没有
        List<CardModel> reusable = [];    // 只有战斗内临时附魔（会被清掉重随机）

        foreach (CardModel card in hand)
        {
            if (HasPermanentEnchantment(card))
                continue;                       // ① 永久附魔：不碰

            if (card.Enchantment == null)
            {
                if (ValidEnchantments(card).Count > 0)
                    untouched.Add(card);        // ② 首选
                continue;
            }

            reusable.Add(card);                 // ③ 次选（上面已排除永久附魔，所以这里只可能是临时的）
        }

        List<CardModel> picked = [.. untouched.TakeRandom(count, player.RunState.Rng.Niche)];
        if (picked.Count < count)
        {
            picked.AddRange(reusable.TakeRandom(count - picked.Count, player.RunState.Rng.Niche));
        }

        return picked;
    }

    // 这张牌身上那层附魔算不算"永久附魔"（牌组本体上就有）。
    // 与 HasTemporaryEnchantment 互为补集：有附魔、又不是临时的 → 永久，本牌一律不碰。
    private static bool HasPermanentEnchantment(CardModel card)
        => card.Enchantment != null && !HasTemporaryEnchantment(card);

    // 给一张牌随机附一个附魔；附上了返回 true（没有可用附魔时返回 false）
    private static bool EnchantRandomly(Player player, CardModel card)
    {
        // 1. 先清掉"战斗内临时附魔"（永久附魔保留不动），并记下它 ——
        //    万一清完发现没有任何可选项（理论上到不了），就把它原样补回去，别让这张牌白亏一个附魔。
        EnchantmentModel? cleared = ClearTemporaryEnchantment(card);

        // 2. 再随机附一个新的
        List<EnchantmentModel> options = ValidEnchantments(card);
        if (options.Count == 0)
        {
            if (cleared != null)
                CardCmd.Enchant(cleared.ToMutable(), card, cleared.Amount);
            return false;
        }

        // 随机用引擎的确定性随机流（Niche 是官方留给"一次性杂项随机"的流，两端一致；
        // 与「骰子18」选骰子点数、官方「迅捷」遗物挑附魔牌同一套）。
        EnchantmentModel chosen = options[player.RunState.Rng.Niche.NextInt(options.Count)];

        // 引擎要求传"可变实例"（官方所有调用都传 ToMutable()/MutableClone()）。
        // CardCmd.Enchant = 挂上附魔 + 跑钩子，卡面文字/图标会自动刷新。
        CardCmd.Enchant(chosen.ToMutable(), card, EnchantAmount);
        return true;
    }

    // 这张牌身上的附魔算不算"战斗内临时附魔"？
    // 判据：牌组本体（DeckVersion）上没有**同类型**的附魔 —— 本体没有、牌上却有，只可能是战斗内临加上去的
    // （可能是本牌上一轮附的，也可能是别的东西在战斗里附的）；
    // 本体上有的（尘封古籍、事件给的永久附魔）则算永久附魔，本牌不去动它。
    // 战斗中生成的牌没有本体（DeckVersion == null），它身上的附魔一律算临时。
    private static bool HasTemporaryEnchantment(CardModel card)
        => card.Enchantment != null
           && card.DeckVersion?.Enchantment?.GetType() != card.Enchantment.GetType();

    // 清掉战斗内临时附魔（永久附魔不动）。
    // ⚠ 官方 CardCmd.ClearEnchantment 只做「Enchantment.ClearInternal() + 置空」，
    //   **不会**还原 OnEnchant 改过的关键字 —— 池子里会动关键字的有两个：
    //     · Steady：OnEnchant 给牌加【保留】→ 不清掉就会永远留在牌上；
    //     · SoulsPower：OnEnchant 把牌的【消耗】删掉 → 不补回来这张牌就永远没有【消耗】了。
    //   所以清完之后按"牌组本体/模型原本有没有这个关键字"对齐一次（见 RestoreKeywordsClearedBy）。
    private static EnchantmentModel? ClearTemporaryEnchantment(CardModel card)
    {
        if (!HasTemporaryEnchantment(card) || card.Enchantment is not { } cleared)
            return null;

        CardCmd.ClearEnchantment(card);
        RestoreKeywordsClearedBy(cleared, card);
        return cleared;
    }

    // 把"上面那两个附魔动过的关键字"按本体状态补回来（本体/模型本来就有就不动它）。
    private static void RestoreKeywordsClearedBy(EnchantmentModel cleared, CardModel card)
    {
        switch (cleared)
        {
            case Steady when !BaseHasKeyword(card, CardKeyword.Retain):
                card.RemoveKeyword(CardKeyword.Retain);
                break;
            case SoulsPower when BaseHasKeyword(card, CardKeyword.Exhaust):
                card.AddKeyword(CardKeyword.Exhaust);
                break;
        }
    }

    // 这张牌"本体"（牌组里的那张；没有本体就用模型）是否带该关键字。
    // 只看 KeywordSources.Local（卡牌自身的关键字），不看战斗里别的能力全局给的。
    private static bool BaseHasKeyword(CardModel card, CardKeyword keyword)
    {
        IReadOnlySet<CardKeyword> baseKeywords =
            card.DeckVersion?.GetKeywordsWithSources(KeywordSources.Local)
            ?? ModelDb.GetById<CardModel>(card.Id).GetKeywordsWithSources(KeywordSources.Local);
        return baseKeywords.Contains(keyword);
    }

    // 这张牌当前能接受哪些附魔。用官方 CanEnchant 判定（它已经算进了卡牌类型限制、
    // "已经附了不可叠加的附魔"、"不可打出"等规则），不自己维护一份类型对照表；
    // 再叠一层 IsRandomSafe 挡住"官方附魔自己会崩"的组合。
    private static List<EnchantmentModel> ValidEnchantments(CardModel card)
    {
        List<EnchantmentModel> list = [];
        foreach (Type enchantmentType in EnchantmentPool)
        {
            // ModelDb.Get(Type) 是 private，所以走"类型 → ModelId → 模型"这条公开路径
            ModelId id = ModelDb.GetId(enchantmentType);
            if (ModelDb.GetByIdOrNull<EnchantmentModel>(id) is not { } enchantment)
                continue;

            if (!enchantment.CanEnchant(card))
                continue;
            if (!IsRandomSafe(enchantment, card))
                continue;

            list.Add(enchantment);
        }
        return list;
    }

    // ===== 安全过滤：屏蔽"官方附魔 + 这张牌"会崩的组合 =====
    // 背景：CanEnchant 只判"能不能附"，不判"附了之后打出去会不会崩"。随机附魔要自己兜住这一点。
    //
    // ① Inky（打出时给目标上虚弱）—— 实测崩过：
    //    OnPlay 里 `new ReadOnlySingleElementList<Creature>(cardPlay.Target)` 直接把目标塞进
    //    PowerCmd.Apply；而 CardPlay.Target 的官方注释是 "Null for un-targeted cards"，
    //    防御牌（TargetType.Self）刚好就是无目标 → 塞进去 null → Apply 里 target.CanReceivePowers 空引用。
    //    崩点日志（godot.log）：
    //      Player 1 playing card LIHUOWANG2_CARD_LIHUOWANG2_DEFEND (no target)
    //      NullReferenceException: at PowerCmd.Apply[T](… Creature target …)
    //                                at …Enchantments.Inky.OnPlay(…)
    //    原版从不踩：唯一的来源「刀刃之墨」只把 Inky 附给它自己生成的飞刀（攻击牌、有敌方目标）。
    //    → 只允许附到"能指定敌方目标"的牌上（单体敌人 / 全体敌人）。
    //
    // ② Goopy（给防御牌加【消耗】，格挡随打出次数增长）—— 潜在崩点（该附魔现已从 EnchantmentPool 移除，
    //    这条守卫保留作双保险：以后若有人把它放回池子，也不会崩）。
    //    AfterCardPlayed 里 `base.Amount++` 之后无条件写
    //      if (base.Card.DeckVersion != null) base.Card.DeckVersion.Enchantment.Amount++;
    //    战斗内的手牌都有"牌组本体"（DeckVersion），而本体上没有这个附魔 → Enchantment 是 null → 一打出就空引用。
    //    原版不踩的原因：它只对牌组里的牌（本体即自己，附魔确实在）或没有本体的生成牌生效。
    //    → 只允许附到"没有牌组本体"的牌上（战斗中生成的牌：飞刀、灼烧、破碎虚空斩……），这样那条分支不会执行。
    //
    // ③ PerfectFit（完美契合：洗牌时把这张牌放到抽牌堆顶）—— **体验过滤**（不是防崩，按需求排除）：
    //    对"打完就离开牌组、永远不会被洗回抽牌堆"的牌毫无意义，随机到等于白附：
    //      · 消耗牌（自带【消耗】）：打完进消耗堆；
    //      · 能力牌（CardType.Power）：打完就常驻场上，同样不回抽牌堆。
    //    判定用卡牌**自身**的关键字（BaseHasKeyword，与上面还原关键字同一套判据），
    //    所以"临时附魔把【消耗】加/删掉"骗不过它；战斗里由能力全局授予的【消耗】不算（属于临时状态）。
    //
    // 以后再加附魔进池子，建议先扫一遍它的 OnPlay / AfterCardPlayed 有没有直接解引用
    // cardPlay.Target、Card.DeckVersion.Enchantment、Card.CombatState 这类可能为 null 的东西。
    private static bool IsRandomSafe(EnchantmentModel enchantment, CardModel card)
        => enchantment switch
        {
            Inky => card.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies,
            Goopy => card.DeckVersion == null,
            PerfectFit => !NeverReturnsToDrawPile(card),
            _ => true
        };

    // 打完就不会再被洗回抽牌堆的牌：消耗牌与能力牌（PerfectFit 对它们完全无效）。
    // 消耗判定走 BaseHasKeyword —— 只看卡牌自身（牌组本体/模型）的关键字，不看临时附魔与全局能力给的。
    private static bool NeverReturnsToDrawPile(CardModel card)
        => card.Type == CardType.Power || BaseHasKeyword(card, CardKeyword.Exhaust);
}
