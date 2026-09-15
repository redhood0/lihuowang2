using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 馒头先吃：获得人工制品；但馒头可能会"死"（概率自我消耗）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ManTouEat : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 人工制品层数（1，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 1m)
    ];

    public lihuowang2ManTouEat() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 获得人工制品
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature,
            DynamicVars["Magic"].BaseValue, Owner.Creature, this);

        // 2. 馒头可能"死掉"：未升级 65% 概率自我消耗；升级后只有 20%
        int roll = Random.Shared.Next(100);
        bool willExplode = IsUpgraded ? roll <= 10 : roll <= 75;
        if (willExplode)
            ExhaustOnNextPlay = true;
    }

    // 升级：人工制品 1 → 2（且死亡概率大幅降低，见本地化/OnPlay）
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
