using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2FireDefense : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 2;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 格挡牌（UI 会据此识别）
    public override bool GainsBlock => true;

    // 悬停提示：神火护体效果。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<FireDefensePower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2FireDefense.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：Block = 格挡（20，升级 +6 → 26）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(20m, ValueProp.Move)
    ];

    public lihuowang2FireDefense() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：获得格挡，弃牌堆加入 2 张灼烧，并获得神火护体。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 2. 弃牌堆加入 2 张灼烧（官方状态牌）
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Discard, 2,
            Owner.Creature.Player);

        // 3. 获得神火护体（本回合受击时点燃所有敌人，回合开始消失）
        await PowerCmd.Apply<FireDefensePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    // 升级：格挡 20 → 26
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(6);
    }
}
