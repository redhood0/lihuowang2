using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 定身符：2 费稀有技能，打出后消耗；「保留」是**升级后**才获得的（见 OnUpgrade）。
// 击晕一个非 BOSS 的敌人（它下一个回合直接跳过），然后抽 1 张牌。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DingShenFu : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 基础版只有「消耗」；「保留」是升级时才加上去的（见 OnUpgrade 的 AddKeyword）：
    //   · Exhaust：打出后消耗；
    //   · Retain（升级后）：回合结束时不弃掉，留在手里等下回合；
    //     引擎的 ShouldRetainThisTurn 直接读这个关键字，不用额外代码。
    // 卡面上的关键字标记由引擎按实例的关键字自动显示，不用写进本地化文本。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Draw = 抽牌数（1；升级只降费，不改抽牌）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Draw", 1m)
    ];

    // 悬停提示：描述里的「击晕」。
    // 用官方「口哨」（Whistle）那张牌同款的 StunIntent.GetStaticHoverTip()：
    // 文案取 intents 表的 STUN.title/description，图标直接取常驻的 intent_atlas
    // （官方注释说明：不要走 PreloadManager.Cache，那会在换房间时被判成"漏缓存"的资源而被卸载）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [StunIntent.GetStaticHoverTip()];

    public lihuowang2DingShenFu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // ===== 「非 BOSS 单位」怎么判（两条独立信号，满足任一条就算 BOSS）=====
    // 引擎里**没有**"某只怪是 BOSS"的标记：MonsterModel 没这个字段，Creature.StunInternal 也不拦 BOSS
    // （官方「口哨」Whistle 就是无条件击晕目标）。所以这里自己判，并且**不用单一信号**——
    // 只按房间判会漏掉"BOSS 出现在非 BOSS 房"的情况（调试命令直接生成 BOSS、事件里刷出来的 BOSS），
    // 这正是之前 BOSS 也被晕住的原因。
    //
    // 信号 A（房间）：这场遭遇战是 BOSS 房 —— EncounterModel.RoomType == RoomType.Boss
    //   （CombatRoom.RoomType => Encounter.RoomType；模组「监天司令牌」判 BOSS 档赏金用的是同一条）。
    // 信号 B（这只怪本身）：目标是**本幕 BOSS 的那几只怪**之一 ——
    //   取 Act.BossEncounter / SecondBossEncounter 的 AllPossibleMonsters（= 该遭遇会出的怪），按怪物 Id 比对。
    //   已核对官方数据：所有真实 BOSS 的怪都**只**出现在 BOSS 遭遇里，不会误伤普通怪
    //   （唯一复用怪的是测试用的 MockBossEncounter / BigDummy，正式游戏不会遇到）。
    // 两条都用 = BOSS 无论在哪都晕不住；代价是 BOSS 房里的杂兵也判成 BOSS（宁可少晕，不可晕错）。
    private bool IsBoss(Creature target)
    {
        ICombatState? combatState = Owner?.Creature.CombatState;
        if (combatState?.Encounter?.RoomType == RoomType.Boss)
            return true;

        MonsterModel? monster = target.Monster;
        var act = Owner?.RunState?.Act;
        if (monster == null || act == null)
            return false;

        return IsBossEncounterMonster(act.BossEncounter, monster)
            || IsBossEncounterMonster(act.SecondBossEncounter, monster);
    }

    // 这只怪是否属于该 BOSS 遭遇（按 Id 比，Id 是 ModelId，用 Equals 走值比较）
    private static bool IsBossEncounterMonster(EncounterModel? encounter, MonsterModel monster)
        => encounter != null && encounter.AllPossibleMonsters.Any(m => m.Id.Equals(monster.Id));

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;

        // 1. 击晕：目标的下个回合会走引擎的 STUNNED 状态（不行动），并播自带的击晕特效。
        //    目标是 BOSS（房间或怪物本身任一条命中，见 IsBoss）就跳过这一步；
        //    注意下面照样抽牌，所以对 BOSS 用也不会完全空过。
        if (!IsBoss(target))
            await CreatureCmd.Stun(target);

        // 2. 抽 1 张牌（晕没晕到都抽，与卡面文字一致）
        Player? player = Owner.Creature.Player;
        if (player != null)
            await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, player);
    }

    // 升级：获得「保留」（费用那一行现在是注释状态，保持原样没动）。
    // AddKeyword 改的是这张牌的实例（CardModel.AddKeyword → 实例的 LocalKeywords），
    // 所以基础版没有保留、升过级的才有；卡面的关键字标记由引擎自动刷新（KeywordsChanged）。
    protected override void OnUpgrade()
    {
        // EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Retain);
    }
}
