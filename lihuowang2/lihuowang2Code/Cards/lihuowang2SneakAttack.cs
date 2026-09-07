using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
public class lihuowang2SneakAttack : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 伤害 10，升级 +3
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Move)
    ];

    public lihuowang2SneakAttack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 若目标已有易伤：额外 +3 伤害并再给 1 层易伤
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;
        decimal damage = DynamicVars.Damage.BaseValue;

        if (target.GetPower<VulnerablePower>() != null)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, Owner.Creature, this);
            damage += 3m;
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
