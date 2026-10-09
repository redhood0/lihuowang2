using System;
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
// 费用固定 2（升级不改费用）；升级只提升回复量：3 → 5 点。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2YangShouDan : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    private const int MaxLifespan = 120;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 每次提高的最大生命（3，升级 +2 → 5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 3m)
    ];

    public lihuowang2YangShouDan() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;
        int missing = MaxLifespan - self.MaxHp;
        if (missing <= 0)
            return; // 已达阳寿上限

        // 不超过阳寿上限：差多少补多少。
        // （原来这里是写死的 "current < 115" —— 那是按"每次固定 5 点"推出来的魔数，
        //   现在 3/5 两种数值都要支持，所以改成按当前回复量算。）
        int gain = Math.Min((int)DynamicVars["Magic"].BaseValue, missing);
        await CreatureCmd.GainMaxHp(self, gain);
    }

    // 升级：最大生命 3 → 5（费用不变，仍是 2）
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(2);
    }
}
