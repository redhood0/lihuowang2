using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
public class lihuowang2DaqianRibs : ModCardTemplate
{
    // 基础耗能（升级后 -1 → 1）
    private const int energyCost = 2;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AllEnemies：全体敌人，无需选目标）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 10m;

    // 大千录 tag
    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu
    ];

    // 悬停时展示给予的易伤、虚弱能力说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<WeakPower>(),
        ];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianRibs.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：
    // Damage = 对每个敌人造成的伤害（25）；Vulnerable = 易伤层数（1）；Weak = 虚弱层数（1）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(25m, ValueProp.Move),
        // PowerVar 默认变量名是 power 类型名（VulnerablePower / WeakPower），
        // 模板 DynamicVars.Vulnerable / DynamicVars.Weak 也按此名访问，二者必须保持一致。
        new PowerVar<VulnerablePower>(2m),
        new PowerVar<WeakPower>(2m)
    ];

    // 默认关键字：保留、消耗（此牌打出后自身也会进消耗堆）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain, CardKeyword.Exhaust
    ];

    public lihuowang2DaqianRibs() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 自己失去 10 点生命（Unblockable：无视格挡的真实 HP 损失）。
        //    放在最前面：若血量不够会先死亡，后续效果不再执行。
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 玩家从手牌选 1 张牌来消耗（此牌自身已带 Exhaust，打出后会自动进消耗堆）
        IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
            filter: null,
            source: this);

        CardModel? card = selected.FirstOrDefault();
        if (card != null)
            await CardCmd.Exhaust(choiceContext, card);
        
        // 3. 给所有敌人施加易伤和虚弱
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemies,
            DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<WeakPower>(choiceContext, enemies,
            DynamicVars.Weak.BaseValue, Owner.Creature, this);
        
        // 4. 对所有敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .Execute(choiceContext);
    }

    // 升级后的效果逻辑：费用 2 → 1
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
