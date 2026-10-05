using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using lihuowang2.Characters;
using lihuowang2.Tags;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 以假修真：消耗 1 张手牌并获得 1 点能量；若消耗的是诅咒/状态，额外把一张随机无色牌（本回合 0 费）加入手牌。
// 升级只让"获得的能量"从 1 变成 2；【消耗】在升级后**照样保留**（升级不再移除它）。
// 带【修真】tag：打出它会给「一炁·心素」累积修真进度。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2YiJiaXiuZhen : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 修真 tag（打出它会累计「一炁·心素」的修真进度；纯机制标记，不显示在卡面，不需要本地化文案）
    protected override HashSet<CardTag> CanonicalTags => [
        XiuZhenTags.XiuZhen
    ];

    // 关键字：消耗（基础与升级后都保留 —— 升级效果只加能量，不再移除消耗）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Energy = 获得的能量（1，升级 +1 → 2）。卡面描述用的是 {Energy:energyIcons()}，
    // 这个格式化器只认 EnergyVar（要读它的 PreviewValue 和 ColorPrefix 来决定画几个、什么颜色的能量图标），
    // 变量缺失或声明成普通 DynamicVar 都会抛异常，导致整条描述退化成未替换的原文。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new EnergyVar(2)
    ];

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

        // ⚠ 必须用 CardCmd.Exhaust，不能用 CardPileCmd.Add(card, PileType.Exhaust)：
        //   后者只是把牌挪进消耗堆，不会调用 Hook.AfterCardExhausted ——
        //   于是「我没病（被消耗时给疯狂）」「炼气（被消耗时给能量）」「触手系列（被消耗时回血）」
        //   这类"被消耗时触发"的牌全部哑火。CardCmd.Exhaust 内部 = 挪进消耗堆 + 记历史 + 触发钩子。
        await CardCmd.Exhaust(choiceContext, chosen);

        // 2. 获得能量（基础 1，升级后 2 —— 走 DynamicVars，与卡面显示同源）
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
        // await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);


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

    // 升级：获得的能量 +1（1 → 2）。
    // ⚠ 不要再写成 RemoveKeyword(CardKeyword.Exhaust)：【消耗】是这张牌的设定，升级后要保留。
    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1);
    }
}
