using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Tags;
using STS2RitsuLib.CardTags; 
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianArm : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AnyEnemy：需要玩家选择一个敌人）
    private const TargetType targetType = TargetType.AnyEnemy;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 10m;

    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu,           // 自定义的大千录 tag
        // CardTag.Strike,            // 想加原版 tag 也可以在这写
    ];
    
    // 悬停时展示给予的易伤能力说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<VulnerablePower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianArm.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：
    // Damage = 造成的伤害（27，升级 +4 → 31）；Vulnerable = 给予的易伤层数（升级后 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(27m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m)
    ];

    // 默认关键字：保留、消耗（此牌打出后自身也会进消耗堆）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain, CardKeyword.Exhaust
    ];

    public lihuowang2DaqianArm() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 玩家从手牌选 1 张牌来消耗（此牌自身已带 Exhaust，打出后会自动进消耗堆）
        IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
            filter: null,
            source: this);

        CardModel? card = selected.FirstOrDefault();
        if (card != null)
            await CardCmd.Exhaust(choiceContext, card);

        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 2. 对目标造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 3. 给予目标易伤
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
            DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);

        // 4. 自己失去 10 点生命（Unblockable：无视格挡的真实 HP 损失）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    // 升级后的效果逻辑：伤害 27 → 31；易伤 1 → 2 层
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars.Vulnerable.UpgradeValueBy(1);
    }
}
