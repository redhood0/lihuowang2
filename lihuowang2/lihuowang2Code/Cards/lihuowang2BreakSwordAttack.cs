using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
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

    // Damage = 伤害（6，升级 +3）；Weak = 目标正在攻击时给予的虚弱（1，升级 +1 → 2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("Weak", 1m)
    ];

    public lihuowang2BreakSwordAttack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 破剑式：造成基础伤害；若目标本回合的意图是攻击（含单次/多次攻击），额外造成 6 点伤害并施加 1 层虚弱。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;

        // 读取敌人意图：Monster.NextMove.Intents 里是否有 AttackIntent（Single/MultiAttack 都是它的子类）。
        bool intendsToAttack = target.Monster?.NextMove?.Intents.Any(intent => intent is AttackIntent) ?? false;

        if (intendsToAttack)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target,
                DynamicVars["Weak"].BaseValue, Owner.Creature, this);

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue + 6m)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
        else
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Weak"].UpgradeValueBy(1);
    }
}
