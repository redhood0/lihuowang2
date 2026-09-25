using System.Collections.Generic;
using System.Threading.Tasks;
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

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MoneyBack : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 2;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Rare;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 格挡牌（UI 会据此识别）
    public override bool GainsBlock => true;

    // 悬停提示：铜钱效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<MoneyPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2MoneyBack.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Factor = 每枚铜钱的格挡/金币倍率（16，升级 +4 → 20）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(20m, ValueProp.Move)
    ];

    public lihuowang2MoneyBack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 没有铜钱时无法打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.GetPower<MoneyPower>() != null;
        }
    }

    // 打出：需要拥有铜钱。获得「倍率×铜钱数」的格挡与金币，随后移除全部铜钱。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        MoneyPower? copper = Owner.Creature.GetPower<MoneyPower>();
        if (player == null || copper == null)
            return; // 没有铜钱时无法结算

        decimal factor = DynamicVars.Block.BaseValue * copper.Amount;

        // 1. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(factor, ValueProp.Move), cardPlay);

        // 2. 获得金币
        await PlayerCmd.GainGold(factor, player);

        // 3. 移除铜钱
        await PowerCmd.Remove(copper);
    }

    // 升级：倍率 16 → 20
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(8);
    }
}
