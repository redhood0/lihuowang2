using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 阳寿丹：永久提高最大生命（上限 120），消耗。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2YangShouDan : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    private const int MaxLifespan = 120;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 每次提高的最大生命（5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 5m)
    ];

    public lihuowang2YangShouDan() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;
        int current = self.MaxHp;
        int gain;
        if (current < 115)
            gain = (int)DynamicVars["Magic"].BaseValue;
        else if (current < MaxLifespan)
            gain = MaxLifespan - current;
        else
            return; // 已达阳寿上限

        await CreatureCmd.GainMaxHp(self, gain);
    }

    // 升级：费用 3 → 2
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
