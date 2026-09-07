using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 铜钱剑·避邪：花钱 → 消耗 1 张手牌；若消耗的是诅咒则格挡翻倍，并攒 1 枚铜钱。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MoneyAgainstGhost : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override bool GainsBlock => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<MoneyPower>()];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Block = 每次获得的格挡（6，升级 +2）；GoldCost = 消耗金币（10）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("GoldCost", 10m)
    ];

    public lihuowang2MoneyAgainstGhost() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 金币不足时无法打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature?.Player == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.Player.Gold >= DynamicVars["GoldCost"].BaseValue;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        decimal goldCost = DynamicVars["GoldCost"].BaseValue;
        if (player.Gold < goldCost)
            return;

        // 1. 花钱
        await PlayerCmd.LoseGold(goldCost, player);

        // 2. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 3. 消耗 1 张手牌
        CardModel? chosen = null;
        List<CardModel> handCards = PileType.Hand.GetPile(player).Cards.ToList();
        if (handCards.Count == 1)
        {
            chosen = handCards[0];
        }
        else if (handCards.Count > 1)
        {
            var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1);
            chosen = (await CardSelectCmd.FromHand(choiceContext, player, prefs, _ => true, this)).FirstOrDefault();
        }

        if (chosen != null)
        {
            await CardPileCmd.Add(chosen, PileType.Exhaust);

            // 4. 消耗的是诅咒：再获得一次格挡（原版效果）
            if (chosen.Type == CardType.Curse)
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }

        // 5. 获得 1 枚铜钱（上限 5）
        await MoneyPower.ApplyMoney(choiceContext, Owner.Creature, Owner.Creature, this);
    }

    // 升级：格挡 6 → 8
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
    }
}
