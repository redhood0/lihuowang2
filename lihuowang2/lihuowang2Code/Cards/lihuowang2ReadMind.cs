using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 读心：预读敌人意图。准备攻击 → 格挡；否则 → 力量。
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

    // Block = 敌准备攻击时获得的格挡（5，升级 +3）；Magic = 否则获得的力量（1，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(5m, ValueProp.Move),
        new DynamicVar("Magic", 1m)
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
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature,
                DynamicVars["Magic"].BaseValue, Owner.Creature, this);
        }
    }

    // 升级：格挡 5 → 8；力量 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
