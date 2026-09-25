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
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 李岁（狗形态）：固有。获得黑太岁+人工制品；每回合对全体敌人 AOE；触手牌增益。
// [RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Lisui_dog : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Damage = 每回合全体 AOE 伤害（3，升级 +1）；Boost = 触手伤害/格挡加成（2，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Damage", 3m),
        new DynamicVar("Boost", 2m)
    ];

    public lihuowang2Lisui_dog() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;

        // 1. 黑太岁 + 人工制品
        await PowerCmd.Apply<Lihuowang2HeitaisuiPower>(choiceContext, self, 1m, self, this);
        await PowerCmd.Apply<ArtifactPower>(choiceContext, self, 1m, self, this);

        // 2. 狗形态本体（AOE + 触手增益）
        await PowerCmd.Apply<Lihuowang2LisuiPower>(choiceContext, self,
            DynamicVars["Boost"].BaseValue, self, this);
        Lihuowang2LisuiPower? lisui = self.GetPower<Lihuowang2LisuiPower>();
        if (lisui != null)
        {
            lisui.Damage = DynamicVars["Damage"].BaseValue;
            lisui.Boost = DynamicVars["Boost"].BaseValue;
        }
    }

    // 升级：AOE 伤害 +1；触手加成 +1
    protected override void OnUpgrade()
    {
        DynamicVars["Damage"].UpgradeValueBy(1);
        DynamicVars["Boost"].UpgradeValueBy(1);
    }
}
