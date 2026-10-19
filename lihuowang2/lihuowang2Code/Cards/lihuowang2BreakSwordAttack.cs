using System.Collections.Generic;
using System.Threading.Tasks;
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
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BreakSwordAttack : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停时展示“虚弱”的关键字说明（与断臂展示“易伤”的做法一致）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<WeakPower>()];

    // Damage = 伤害（9，升级 +1 → 10）；Weak = 目标准备攻击时给予的虚弱（1，升级 +1 → 2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(9m, ValueProp.Move),
        new DynamicVar("Weak", 1m)
    ];

    public lihuowang2BreakSwordAttack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 破剑式：造成 {Damage} 点伤害；若目标本回合准备攻击，再给予 {Weak} 层虚弱。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")   // 命中特效：斩击（破剑式）
            .Execute(choiceContext);

        // 虚弱降低的是目标**打出**的伤害，不会像易伤那样反过来放大本牌这一下，
        // 所以放在伤害之后给不影响数值；目标若已被这一下打死就不再施加（没意义）。
        if (!target.IsDead && IsIntendingToAttack(target))
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target,
                DynamicVars["Weak"].BaseValue, Owner.Creature, this);
        }
    }

    // 目标本回合是否"准备攻击" —— 与「偷袭」用同一套判定（官方 MonsterModel.IntendsToAttack，
    // 即 NextMove 的意图里含 Attack 或 DeathBlow）。
    // 比原来自己判断 `intent is AttackIntent` 更稳：官方这个还覆盖"致命一击"，
    // 两张牌的判定口径也就统一了。
    private static bool IsIntendingToAttack(Creature target)
    {
        return target.Monster?.IntendsToAttack ?? false;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);    // 9 → 10
        DynamicVars["Weak"].UpgradeValueBy(1);   // 1 → 2
    }
}
