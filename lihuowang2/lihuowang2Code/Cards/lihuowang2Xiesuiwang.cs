using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 血衰王：消耗 1 张手牌，伤害 =（消耗堆诅咒数 + 1）× Magic。
// 未升级：随机消耗；升级后：自己选一张。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Xiesuiwang : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 每张诅咒提供的伤害（5，升级 +2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 5m)
    ];

    public lihuowang2Xiesuiwang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 消耗 1 张手牌
        List<CardModel> handCards = PileType.Hand.GetPile(player).Cards.ToList();
        CardModel? toExhaust = null;
        if (handCards.Count > 0)
        {
            if (IsUpgraded)
            {
                toExhaust = (await CardSelectCmd.FromHand(
                    choiceContext, player,
                    new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
                    filter: null, source: this)).FirstOrDefault();
            }
            else
            {
                toExhaust = handCards[Random.Shared.Next(handCards.Count)];
            }
        }

        if (toExhaust != null)
            await CardCmd.Exhaust(choiceContext, toExhaust);

        // 2. 计算消耗堆里的诅咒数并攻击
        int curseCount = PileType.Exhaust.GetPile(player).Cards.Count(c => c.Type == CardType.Curse);
        decimal damage = (1m + curseCount) * DynamicVars["Magic"].BaseValue;

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    // 升级：每张诅咒伤害 5 → 7
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(2);
    }
}
