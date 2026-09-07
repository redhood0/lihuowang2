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

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MoonWindSword : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Damage = 伤害（11，升级 +5 → 16）；Draw = 击杀后抽牌（1，升级 +1 → 2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(11m, ValueProp.Move),
        new DynamicVar("Draw", 1m)
    ];

    public lihuowang2MoonWindSword() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 疯癫时无法打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.GetPower<CrazyPower>() == null;
        }
    }

    // 造成伤害；若因此击杀敌人，获得 1 点能量并抽牌
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        if (target.IsDead)
        {
            if (Owner.Creature.Player != null)
                await PlayerCmd.GainEnergy(1m, Owner.Creature.Player);
            await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner.Creature.Player!);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5);
        DynamicVars["Draw"].UpgradeValueBy(1);
    }
}
