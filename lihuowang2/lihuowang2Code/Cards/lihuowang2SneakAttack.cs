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
public class lihuowang2SneakAttack : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 伤害 10，升级 +5（10 → 15）。
    // ValueProp.Move = 卡牌的普通伤害（会被力量、易伤等加成），不是"固定伤害"。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Move)
    ];

    // 悬停提示：描述里点名了官方能力「易伤」，按项目约定补上它的 hover。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<VulnerablePower>()];

    public lihuowang2SneakAttack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 造成 {Damage} 点伤害；若目标没有攻击意图，再额外给 1 层易伤。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_dramatic_stab")   // 命中特效：突刺（偷袭）
            .Execute(choiceContext);

        // 易伤必须在**打完之后**再给：本牌伤害用的是 ValueProp.Move（= 卡牌普通伤害，
        // 会被易伤加成放大）。若先上易伤，这一下就变成 15 而不是 10，与卡面不符。
        // 另：目标若已被这一下打死就不给了 —— 给死人上易伤没意义，也避免对已出场的单位施加能力。
        if (!target.IsDead && !IsIntendingToAttack(target))
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, Owner.Creature, this);
        }
    }

    // 目标本回合是否在"准备攻击"。
    // 用官方 MonsterModel.IntendsToAttack：NextMove 的意图里含 Attack 或 DeathBlow 即算准备攻击。
    // 非怪物单位（例如联机时的玩家）没有 Monster，一律按"没有攻击意图"处理。
    private static bool IsIntendingToAttack(Creature target)
    {
        return target.Monster?.IntendsToAttack ?? false;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5);   // 10 → 15
    }
}
