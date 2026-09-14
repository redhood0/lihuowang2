using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 关口：弃 1 张手牌 → 预见 magic 张（可弃任意）→ 抽 1。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Guankou : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 关键字：预见。像原版「消耗」那样把关键字交给引擎，悬停卡牌时自动带上关键词说明。
    // 关键词本体在 Keywords/Lihuowang2Keywords.cs 注册，说明文本在 card_keywords 表。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [Lihuowang2ForeseeKeyword.Value];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 1m)
    ];

    public lihuowang2Guankou() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 丢弃 1 张手牌
        var discard = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1, 1);
        CardModel? chosen = (await CardSelectCmd.FromHandForDiscard(
            choiceContext, player, discard, null, this)).FirstOrDefault();
        if (chosen == null)
            return;

        await CardPileCmd.Add(chosen, PileType.Discard);

        // 2. 预见 magic 张
        await Lihuowang2ScryUtil.ScryAndDiscardAny(choiceContext, player, (int)DynamicVars["Magic"].BaseValue);

        // 3. 抽 1
        await CardPileCmd.Draw(choiceContext, 1m, player);
    }

    // 升级：预见 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
