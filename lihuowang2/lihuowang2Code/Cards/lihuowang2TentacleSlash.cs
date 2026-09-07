using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 触手·斩：需要黑太岁才能打出；斩两刀；被消耗时回血（由黑太岁消耗触发回血，见 Lihuowang2HeitaisuiPower）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2TentacleSlash : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(5m, ValueProp.Move)
    ];

    public lihuowang2TentacleSlash() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 需要黑太岁才能打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.GetPower<Lihuowang2HeitaisuiPower>() != null;
        }
    }

    // 斩两刀
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .WithHitCount(2)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    // 升级：伤害 5 → 7
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}
