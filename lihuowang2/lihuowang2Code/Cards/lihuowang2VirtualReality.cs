using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Keywords;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 虚拟？现实：能力牌（稀有）。
// 效果：每当你预见，对一个随机敌人造成 {Damage} 点伤害。
// 触发点见 Cards/Lihuowang2ScryUtil.cs（IForeseeListener），能力本体见 Powers/VirtualRealityPower。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2VirtualReality : ModCardTemplate
{
    // 费用 / 类型 / 稀有度。费用没在需求里指定，这里按"稀有能力的常见档位"取 2，改这一个常量即可。
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 卡图：images/cards/lihuowang2VirtualReality.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里点名了模组关键字「预见」。
    // 注意：这张牌本身并不"具有"预见，所以不写进 CanonicalKeywords（那会在卡面上挂出关键字标签），
    // 只把它做成一条 hover 说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromKeyword(Lihuowang2ForeseeKeyword.Value)];

    // Damage = 每次预见造成的伤害（6，升级 +2 → 8）。
    // ⚠ 这里用 Unpowered 是为了和实际结算一致：能力打的是固定伤害（同官方「黑洞」），
    //   卡面预览也就不该被力量/易伤等修正放大。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(6m, ValueProp.Unpowered)
    ];

    public lihuowang2VirtualReality() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：施加能力。层数 = 每次预见的伤害（再打一张会叠加，每次预见打得更疼）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<VirtualRealityPower>(choiceContext, Owner.Creature,
            DynamicVars.Damage.BaseValue, Owner.Creature, this);
    }

    // 升级：伤害 6 → 8
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}
