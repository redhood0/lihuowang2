using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 触手·食鬼：需要黑太岁才能打出；吃掉手里所有诅咒并抽等量牌 +1（升级后诅咒与状态都吃）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2TentacleEatGhost : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2TentacleEatGhost() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 需要黑太岁才能打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.GetPower<Lihuowang2HeitaisuiPower>() != null;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 找出手里要吃的牌：基础只吃诅咒；升级后诅咒与状态都吃
        List<CardModel> toEat = PileType.Hand.GetPile(player).Cards
            .Where(c => c.Type == CardType.Curse || (IsUpgraded && c.Type == CardType.Status))
            .ToList();

        // 先吃光
        foreach (CardModel card in toEat)
            await CardCmd.Exhaust(choiceContext, card);

        // 每吃 1 张抽 1，外加固定抽 1
        await CardPileCmd.Draw(choiceContext, 1m + toEat.Count, player);
    }

    // 升级：无数值变化（描述随升级启用吃状态牌）
    protected override void OnUpgrade()
    {
    }
}
