using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 我没病：一张无法打出的"牌"。被消耗时（不限于黑太岁，任何来源消耗它都会触发）：
// 疯狂进手 + 自身复制体随机洗回抽牌堆。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ImNotSick : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的「疯狂」牌。用它自带的悬停（含关键词）版本，
    // 所以疯狂身上的「消耗」也会顺带出现在本牌的 hover 里。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<lihuowang2Madness>();

    public lihuowang2ImNotSick() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 不能打出（效果全在"被消耗"上）
    protected override bool IsPlayable => false;

    // 被消耗时触发。
    // 引擎会把战斗内所有卡牌都算作 hook 监听者（见 CombatState.IterateHookListeners 遍历各牌堆），
    // 所以这里能收到"自己被抓去消耗"的回调，不依赖是哪个效果在消耗它。
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card != this)
            return;

        Player player = Owner;
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null)
            return;

        // 疯狂进手（升级版的「我没病」给的是升级过的疯狂：费用 1 → 0）
        CardModel? madness = combatState.CreateCard<lihuowang2Madness>(player);
        if (madness != null)
        {
            if (IsUpgraded)
                CardCmd.Upgrade(madness);
            await CardPileCmd.AddGeneratedCardToCombat(madness, PileType.Hand, player);
        }

        // 自身复制体洗回抽牌堆（随机位置）
        // CardModel? clone = combatState.CreateCard<lihuowang2ImNotSick>(player);
        // if (clone != null)
        //     await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Draw, player,
        //         CardPilePosition.Random);

        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
    }
}
