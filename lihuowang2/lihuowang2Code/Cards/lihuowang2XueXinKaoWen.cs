using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 血新拷问：1 费稀有攻击。对目标连打 6 下、每下 1 点；初始带「消耗」，升级后不再消耗。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XueXinKaoWen : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 固定 6 段。段数不随升级变化（升级只去掉消耗），所以写成常量而不是 DynamicVar。
    private const int hitCount = 6;

    // 初始为消耗牌，升级后不再消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Damage = 每段伤害（1；本卡升级不提升伤害）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(1m, ValueProp.Move)
    ];

    public lihuowang2XueXinKaoWen() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 对目标连打 6 下，每下 1 点
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .WithHitCount(hitCount)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    // 升级：去除「消耗」词条（伤害仍是 1×6）。
    // 卡面上的消耗标记由 CanonicalKeywords 决定，RemoveKeyword 后会自动消失，不需要额外改文案。
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
