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
public class lihuowang2ShaQiInside : ModCardTemplate
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

    // 悬停提示：煞气效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ShaQiPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2ShaQiInside.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：ShaQi = 没有煞气时获得的层数（5，升级 +2 → 7）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("ShaQi", 5m)
    ];

    public lihuowang2ShaQiInside() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：已有煞气则翻倍（升级后变为 3 倍）；没有煞气则直接获得 5 层（升级 7 层）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ShaQiPower? shaqi = Owner.Creature.GetPower<ShaQiPower>();
        if (shaqi != null && shaqi.Amount > 0)
        {
            // 翻倍 / 三倍 = 再补上 (倍率 - 1) × 当前层数
            int multiplier = IsUpgraded ? 3 : 2;
            await PowerCmd.Apply<ShaQiPower>(choiceContext, Owner.Creature,
                shaqi.Amount * (multiplier - 1), Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<ShaQiPower>(choiceContext, Owner.Creature,
            DynamicVars["ShaQi"].BaseValue, Owner.Creature, this);
    }

    // 升级：没煞气时的层数 5 → 7；已有煞气时倍率 2 → 3（见 OnPlay）
    protected override void OnUpgrade()
    {
        DynamicVars["ShaQi"].UpgradeValueBy(2);
    }
}
