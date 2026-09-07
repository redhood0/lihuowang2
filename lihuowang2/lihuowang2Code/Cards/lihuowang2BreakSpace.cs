using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Void = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace lihuowang2.Cards;

// 破碎虚空斩：清空所有敌人的格挡并对其造成伤害，随后弃牌堆放入 1 张虚空。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BreakSpace : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(30m, ValueProp.Move)
    ];

    public lihuowang2BreakSpace() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;

        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        foreach (Creature enemy in enemies)
        {
            // 1. 清空格挡
            if (enemy.Block > 0)
                await CreatureCmd.LoseBlock(choiceContext, enemy, enemy.Block, Owner.Creature);

            // 2. 造成伤害
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(enemy)
                .Execute(choiceContext);
        }

        // 3. 弃牌堆放入 1 张虚空
        if (player != null)
            await CardPileCmd.AddToCombatAndPreview<Void>(Owner.Creature, PileType.Discard, 1, player);
    }

    // 升级：伤害 30 → 40
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10);
    }
}
