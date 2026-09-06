using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 炸炉：丹炉炼多后可能炸出的一张诅咒。不能打出；
// 若回合结束时它在手牌中（由丹炉 power 负责结算），会炸毁丹炉并烧你 1 点生命，随后消耗。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2CurseZhalu : ModCardTemplate
{
    private const int energyCost = -2;
    private const CardType type = CardType.Curse;
    private const CardRarity rarity = CardRarity.Curse;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2CurseZhalu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 诅咒无法打出
    protected override bool IsPlayable => false;
}
