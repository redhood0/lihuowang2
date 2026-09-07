using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 以假修真：消耗 1 张手牌并获得 1 点能量；若消耗的是诅咒/状态，额外把一张随机无色牌（本回合 0 费）加入手牌。
// 基础会连自己一起消耗；升级后自己不再消耗。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2YiJiaXiuZhen : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 基础版本打出后也会消耗自己；升级后移除
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2YiJiaXiuZhen() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 选择 1 张手牌消耗
        CardModel? chosen = (await CardSelectCmd.FromHand(
            choiceContext, player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
            filter: null, source: this)).FirstOrDefault();
        if (chosen == null)
            return;

        await CardPileCmd.Add(chosen, PileType.Exhaust);

        // 2. 获得 1 点能量
        await PlayerCmd.GainEnergy(1m, player);

        // 3. 消耗的是诅咒/状态：把一张随机无色牌（本回合 0 费）加入手牌
        if (chosen.Type == CardType.Curse || chosen.Type == CardType.Status)
            await AddRandomColorless(choiceContext, player);
    }

    private async Task AddRandomColorless(PlayerChoiceContext choiceContext, Player player)
    {
        CardPoolModel? colorlessPool = ModelDb.AllSharedCardPools.OfType<ColorlessCardPool>().FirstOrDefault();
        if (colorlessPool == null)
            return;

        List<CardModel> cards = colorlessPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();
        if (cards.Count == 0)
            return;

        CardModel? card = CardFactory
            .GetDistinctForCombat(player, cards, 1, player.RunState.Rng.CombatCardGeneration)
            .FirstOrDefault();
        if (card == null)
            return;

        card.SetToFreeThisTurn(); // 本回合耗能 0
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
    }

    // 升级：不再消耗自己
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
