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
// 注册进官方无色卡池，配合「我没病/我有病」产出。
[RegisterCard(typeof(ColorlessCardPool))]
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

        CardModel chosen = candidates[Random.Shared.Next(candidates.Count)];
        chosen.SetToFreeThisTurn();
    }

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
