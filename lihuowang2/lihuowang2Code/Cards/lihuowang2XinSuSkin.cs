using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 替身人皮：需要携带心素、且生命大于最大生命一半才能打出。
// 自伤一半最大生命，换取/充能【人皮】遗物。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XinSuSkin : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的「替身人皮」（濒死时挡下一次致命伤害并回复一半最大生命，
    // 破损后靠本牌再充能）。FromRelic 返回的是一组提示，所以用展开语法并进列表。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [.. HoverTipFactory.FromRelic<lihuowang2Relic_Renpi>()];

    public lihuowang2XinSuSkin() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 门禁：需要心素遗物 + 生命 > 一半最大生命
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature?.Player == null)
                return true; // 图鉴/预览场景不做限制
            Player player = Owner.Creature.Player;
            if (player.GetRelic<lihuowang2Relic_Xinsu>() == null)
                return false;
            return Owner.Creature.CurrentHp > Owner.Creature.MaxHp / 2;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 获得/充能 人皮
        lihuowang2Relic_Renpi? renpi = player.GetRelic<lihuowang2Relic_Renpi>();
        if (renpi == null)
            await RelicCmd.Obtain<lihuowang2Relic_Renpi>(player);
        else
            renpi.Recharge();

        // 2. 自伤：失去一半最大生命的生命
        decimal loss = Owner.Creature.MaxHp / 2m;
        await CreatureCmd.Damage(choiceContext, Owner.Creature, loss, 
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
