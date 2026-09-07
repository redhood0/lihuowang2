using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 我没病：一张无法打出的"牌"。当被黑太岁等消耗时：疯狂进手 + 自身复制体洗回抽牌堆。
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

    public lihuowang2ImNotSick() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 不能打出（效果全在"被消耗"上，见 Lihuowang2HeitaisuiPower）
    protected override bool IsPlayable => false;
}
