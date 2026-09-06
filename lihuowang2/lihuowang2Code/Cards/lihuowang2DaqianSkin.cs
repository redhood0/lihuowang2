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
using lihuowang2.Tags;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianSkin : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型（对敌人施加减益的辅助牌）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（AllEnemies：全体敌人，无需选目标）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命值（无视格挡的真实 HP 损失）
    private const decimal SelfHpLossAmount = 8m;
    // 「敌人失去力量」的变量名，本地化里用 {StrengthLoss:diff()} 显示；升级后 +2（8 → 10）。
    // 命名与官方 Mangle（ManglePower）一致。
    private const string StrengthLossVarName = "StrengthLoss";

    // 大千录 tag
    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu
    ];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianSkin.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：StrengthLoss = 敌人失去的力量（8，升级 +2 → 10）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar(StrengthLossVarName, 8m)
    ];

    // 默认关键字：保留、消耗（此牌打出后自身也会进消耗堆）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain, CardKeyword.Exhaust
    ];

    public lihuowang2DaqianSkin() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑：先自己失去 8 点生命，再让所有敌人本回合失去力量
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 自己失去 8 点生命（Unblockable）。
        //    放在最前面：若血量不够会先死亡，后续效果不再执行。
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLossAmount,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        // 2. 所有敌人本回合失去 {StrengthLoss} 点力量（官方 Mangle 同款机制）：
        //    Lihuowang2DaqianSkinStrengthDown : TemporaryStrengthPower（IsPositive = false），
        //    内部会自动 Apply StrengthPower(-N)，并在该敌人回合结束时 +N 补回。
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await PowerCmd.Apply<Lihuowang2DaqianSkinStrengthDown>(choiceContext, enemies,
            DynamicVars[StrengthLossVarName].BaseValue, Owner.Creature, this);
    }

    // 升级后的效果逻辑：敌人失去力量 8 → 10
    protected override void OnUpgrade()
    {
        DynamicVars[StrengthLossVarName].UpgradeValueBy(2);
    }
}
