using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 我没病：一张无法打出的"牌"。被消耗时（不限于黑太岁，任何来源消耗它都会触发）：疯狂（未升级）进手。
// 升级版额外把一张「我没病+」洗进【本场战斗的抽牌堆】（随机位置；走 AddGeneratedCardToCombat，带飞牌动画）。
// 注意：升级后的收益在"往抽牌堆塞复制体"，进手的疯狂【不】跟着升级（需求原文就是「将 1 张疯狂加入手牌」）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ImNotSick : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的牌。
    //  - 「疯狂」：基础版和升级版进手的都是未升级的那张，所以固定传 false。
    //  - 「我没病+」：只有升级版才往抽牌堆塞复制体，而塞进去的是升级版，所以这条提示只在升级后出现、并传 true。
    // 注：RitsuLib 的 ExtraHoverTips 每次访问都重新构造列表（不缓存），所以这里读 IsUpgraded 在升级后能正常刷新。
    // 注：FromCardWithCardHoverTips 返回的是「一组提示」，需要展开语法并进列表；
    // FromCard 返回的是单独一条提示，直接当元素写（两者签名不同，混用会编译报错）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        IsUpgraded
            ?
            [
                .. HoverTipFactory.FromCardWithCardHoverTips<lihuowang2Madness>(false)
            ]
            : [.. HoverTipFactory.FromCardWithCardHoverTips<lihuowang2Madness>(false)];

    public lihuowang2ImNotSick() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 不能打出（效果全在"被消耗"上）
    protected override bool IsPlayable => false;

    // 被消耗时触发。
    // 引擎会把战斗内所有卡牌都算作 hook 监听者（见 CombatState.IterateHookListeners 遍历各牌堆），
    // 所以这里能收到"自己被抓去消耗"的回调，不依赖是哪个效果在消耗它。
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card != this)
            return;

        Player player = Owner;
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null)
            return;

        // 升级状态以「被消耗的那张牌」为准：钩子会对全战斗每张牌各调用一次，
        // card 才是真正被抓去消耗的实例；理论上 card 与 this 是同一张，但以 card 为准更稳。
        bool upgraded = card.IsUpgraded || IsUpgraded;

        // 1. 疯狂进手。基础版与升级版都只给【未升级】的疯狂：
        //    升级的收益是"往抽牌堆塞一张我没病+"，而不是提升进手的疯狂品质。
        CardModel? madness = combatState.CreateCard<lihuowang2Madness>(player);
        if (madness != null)
            await CardPileCmd.AddGeneratedCardToCombat(madness, PileType.Hand, player);

        // 2. 升级版专属：把一张「我没病+」洗进【本场战斗的抽牌堆】。
        //
        // ⚠ 是战斗内的临时牌堆（PileType.Draw），不是本局永久牌组（PileType.Deck）：
        //   · 实例必须用 combatState.CreateCard 生成 —— CardPileCmd.Add 对战斗牌堆会校验
        //     "must be added to a CombatState before adding it to this pile"（牌必须已登记进 CombatState）；
        //     RunState.CreateCard 那条路是给永久牌组用的（对照 lihuowang2EventTemplate.ObtainCardToDeck）。
        //   · 效果：本场战斗后续抽牌就能抽到它，战斗结束随战斗牌堆一起消失。
        //
        // 用 AddGeneratedCardToCombat（战斗内生成牌的标准入堆流程）：
        //   · 自带"牌飞向抽牌堆"的动画（不传 skipVisuals，视觉默认开启）；
        //   · 会记录到战斗历史并触发 AfterCardGeneratedForCombat，与官方生成牌一致；
        //   · CardPilePosition.Random 让它随机插进抽牌堆，而不是固定塞到底部。
        // 若你希望再显眼一点，可在 await 之后加一行 CardCmd.Preview(copy, 0.75f)
        // （官方遗物 BiiigHug 就是这个写法：洗入抽牌堆 + 把这张牌明示给玩家）。
        if (upgraded)
        {
            CardModel? copy = combatState.CreateCard<lihuowang2ImNotSick>(player);
            if (copy != null)
            {
                CardCmd.Upgrade(copy); // 复制体与本体同升级状态（塞进去的是「我没病+」）
                await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Draw, player, CardPilePosition.Random);
            }
        }

        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
    }
}
