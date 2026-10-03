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

// 读心：预读敌人意图。准备攻击 → 格挡；否则 → 活力（下一次攻击的伤害加成，打完自动清零）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2ReadMind : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里的「活力」。
    // 活力是官方能力（VigorPower），文案/图标取官方 powers 表，不需要自己写；
    // 与「大力丹」给官方 StrengthPower 挂 hover 的写法一致。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<VigorPower>()];

    // Block = 敌准备攻击时获得的格挡（6，升级 +3）；Magic = 否则获得的活力（2，升级 +1 → 3）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("Magic", 2m)
    ];

    public lihuowang2ReadMind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;
        bool intendsToAttack = target.Monster?.NextMove?.Intents
            .Any(i => i is AttackIntent or MultiAttackIntent or SingleAttackIntent or DeathBlowIntent) ?? false;

        if (intendsToAttack)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
        else
        {
            // 活力是官方能力（VigorPower）：加成下一次攻击的伤害，攻击结算后自动清零，
            // 与「力量」的永久加成不同（所以这里不再用 StrengthPower）。
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature,
                DynamicVars["Magic"].BaseValue, Owner.Creature, this);
        }
    }

    // 升级：格挡 6 → 9；活力 2 → 3
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
