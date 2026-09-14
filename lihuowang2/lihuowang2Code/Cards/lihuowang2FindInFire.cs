using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2FindInFire : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2FindInFire.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Draw = 抽牌数（3，升级 +1 → 4）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Draw", 3m)
    ];

    public lihuowang2FindInFire() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：抽 N 张牌，然后把 2 张灼烧洗入抽牌堆。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 抽牌
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner.Creature.Player!);

        // 2. 洗入 2 张灼烧到抽牌堆（随机位置）
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Draw, 2,
            Owner.Creature.Player, CardPilePosition.Random);
    }

    // 升级：抽牌 3 → 4
    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1);
    }
}
