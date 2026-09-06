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
public class lihuowang2BiGuDan : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2BiGuDan.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Heal = 治疗量（3，升级 +2 → 5）；Energy = 下回合能量（1，升级 +1 → 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Heal", 3m),
        new DynamicVar("Energy", 1m)
    ];

    // 关键字：保留、消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain, CardKeyword.Exhaust
    ];

    public lihuowang2BiGuDan() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：回复生命，下回合获得额外能量
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature,
            DynamicVars["Energy"].BaseValue, Owner.Creature, this);
    }

    // 升级：治疗 3 → 5；能量 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars["Heal"].UpgradeValueBy(2);
        DynamicVars["Energy"].UpgradeValueBy(1);
    }
}
