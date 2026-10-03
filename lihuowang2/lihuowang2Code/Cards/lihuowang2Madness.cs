using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 疯狂（无色）：让手牌中随机一张牌（优先非 0 费）本回合耗能变为 0，打出后消耗。
//
// 注册进引擎的「衍生牌池」TokenCardPool（Shiv / Soul / SovereignBlade 等都在这里），而不是无色卡池：
//   · 无色卡池 ColorlessCardPool 是"可被随机发现"的池子（例：以假修真、官方遗物 OrangeDough 都从这里抽），
//     疯狂放进去就会被「随机无色牌」类效果抽到，而它本该只由「我没病 / 我有病」产出。
//   · TokenCardPool 与无色池外观一致（CardFrameMaterialPath = card_frame_colorless、IsColorless = true），
//     所以卡框、能量图标仍是无色样式，不需要再覆写 VisualCardPool。
[RegisterCard(typeof(TokenCardPool))]
public class lihuowang2Madness : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 关键字：消耗（打出后进消耗堆；卡面会自动追加「消耗」字样）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2Madness() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 衍生牌标记：不能被"战斗内随机生成/发现"类效果抽到。
    // 引擎的发现/生成过滤器 CardFactory.FilterForCombat 的第一项判定就是它：
    //   cards.Where(c => c.CanBeGeneratedInCombat && c.Rarity != Basic && ...)
    public override bool CanBeGeneratedInCombat => false;

    // 衍生牌标记：不进修正器牌池（与官方 AscendersBane 的声明一致）。
    public override bool CanBeGeneratedByModifiers => false;


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        List<CardModel> others = PileType.Hand.GetPile(player).Cards
            .Where(c => c != this)
            .ToList();
        if (others.Count == 0)
            return;

        // 优先挑非 0 费牌，没有则随便挑一张
        List<CardModel> candidates = others
            .Where(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            .ToList();
        if (candidates.Count == 0)
            candidates = others;

        // 多人同步：随机选牌要用引擎的确定性随机流（官方"战斗内随机选牌"流，
        // 官方 True Grit 之类用的就是它）。Random.Shared 各端结果不同 → 报"不同步"。
        CardModel chosen = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
        chosen.SetToFreeThisCombat();
    }

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
