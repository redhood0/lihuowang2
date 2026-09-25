using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Keywords;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 逐卜避荒[符篆]：预见后抽牌。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ZhuBuBiHuang : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 关键字：预见（悬停卡牌时会像「消耗」那样带上关键词说明）。
    // 关键词本体在 Keywords/Lihuowang2Keywords.cs 注册，文本在 card_keywords 表的
    // LIHUOWANG2_KEYWORD_FORESEE.title / .description。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [Lihuowang2ForeseeKeyword.Value];

    // Magic = 预见数（3，升级 +2）；Draw = 抽牌数（1，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 3m),
        new DynamicVar("Draw", 1m)
    ];

    public lihuowang2ZhuBuBiHuang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }
    


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        await Lihuowang2ScryUtil.ScryAndDiscardAny(choiceContext, player, (int)DynamicVars["Magic"].BaseValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, player);
    }

    // 升级：预见 3 → 5；抽 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(2);
        DynamicVars["Draw"].UpgradeValueBy(1);
    }
}
