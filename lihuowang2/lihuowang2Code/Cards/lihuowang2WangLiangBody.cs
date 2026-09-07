using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
public class lihuowang2WangLiangBody : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 2;
    // 卡牌类型（能力牌）
    private const CardType type = CardType.Power;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Rare;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 悬停提示：魍魉之躯效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<WangLiangBodyPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2WangLiangBody.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Power = 每 2 张诅咒获得的力量（1，升级 +1 → 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Power", 1m)
    ];

    public lihuowang2WangLiangBody() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：施加魍魉之躯
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<WangLiangBodyPower>(choiceContext, Owner.Creature,
            DynamicVars["Power"].BaseValue, Owner.Creature, this);
    }

    // 升级：力量 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars["Power"].UpgradeValueBy(1);
    }
}
