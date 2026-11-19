using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 修假（1 费，稀有，技能）：从 3 张「其他角色」的卡牌中选 1 张加入手牌。
// 代价：**选中的那张牌**获得【消耗】【虚无】（本牌自己没有任何关键字，打完进弃牌堆，可重复抽到）。
// 升级：费用 1 → 0（其余不变）。
//
// 实现要点：
//   · 「其他角色」= 除模组角色 lihuowang2Character 之外所有角色的卡池（ModelDb.AllCharacters 各自的 CardPool），
//     只取当前解锁状态与单人/多人限制下可用的牌；
//   · 3 张候选走 CardFactory.GetDistinctForCombat：
//     它会先 FilterForCombat（排除基础/先古/事件牌与不能战斗内生成的牌），
//     再用引擎确定性随机流 CombatCardGeneration 抽取 —— 多人两端抽到的候选一致；
//   · 三选一用 CardSelectCmd.FromChooseACardScreen（官方"至多 3 张里挑 1 张"的标准界面，
//     canSkip: false 表示必须选一张）。
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

    // 候选张数：从这么多张里选 1 张（FromChooseACardScreen 最多支持 3 张）。
    private const int ChoiceCount = 3;

    // 悬停：描述里点名的两个关键字（给的是"选中的那张牌"，不是本牌）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2XiuJia() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 其他角色的卡池：排除模组角色自己。
        // 无色牌 / 诅咒 / 状态不属于任何角色的池子，天然不会被抽到。
        List<CardModel> candidates = ModelDb.AllCharacters
            .Where(c => c is not lihuowang2Character)
            .SelectMany(c => c.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
            return;

        // 抽 3 张互不相同的候选（返回的是"已归属玩家、属于本场战斗"的可变实例，可直接进手牌）。
        List<CardModel> offers = CardFactory
            .GetDistinctForCombat(player, candidates, ChoiceCount, player.RunState.Rng.CombatCardGeneration)
            .ToList();
        if (offers.Count == 0)
            return;

        // 三选一（必须选）：选中的那张加入手牌。
        CardModel? picked = await CardSelectCmd.FromChooseACardScreen(
            choiceContext, offers, player, canSkip: false);
        if (picked == null)
            return;

        // 给选中的这张牌加上【消耗】【虚无】（本牌自己不受影响）。
        // AddKeyword 改的是这张"战斗内实例"的关键字集合，战斗结束随实例一起消失，不会沾到牌组本体。
        // 若它本来就有这两个关键字之一，AddKeyword 是幂等的，不会重复。
        picked.AddKeyword(CardKeyword.Exhaust);
        picked.AddKeyword(CardKeyword.Ethereal);

        await CardPileCmd.AddGeneratedCardToCombat(picked, PileType.Hand, player);
    }

    // 升级：费用 1 → 0。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
