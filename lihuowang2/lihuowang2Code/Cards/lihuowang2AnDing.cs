using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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
public class lihuowang2AnDing : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Heal = 治疗量（4，升级 +1 → 5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Heal", 4m)
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2AnDing() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 回复生命；若处于疯癫则解除疯癫并获得 1 点能量
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);

        CrazyPower? crazy = Owner.Creature.GetPower<CrazyPower>();
        if (crazy != null)
        {
            await PowerCmd.Remove(crazy);
            Player? player = Owner.Creature.Player;
            if (player != null)
                await PlayerCmd.GainEnergy(1m, player);
        }
    }

    // 升级：治疗 4 → 5；获得保留
    protected override void OnUpgrade()
    {
        DynamicVars["Heal"].UpgradeValueBy(1);
        CardCmd.ApplyKeyword(this, CardKeyword.Retain);
    }
}
