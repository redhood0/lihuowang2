using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaLiDan : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
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

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaLiDan.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Block = 格挡（3，升级 +2 → 5）；Str = 力量（2，升级 +1 → 3）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(3m, ValueProp.Move),
        new DynamicVar("Str", 2m)
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaLiDan() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：获得格挡与力量
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature,
            DynamicVars["Str"].BaseValue, Owner.Creature, this);
    }

    // 升级：格挡 3 → 5；力量 2 → 3
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Str"].UpgradeValueBy(1);
    }
}
