using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 幽灵环：自损 5 点生命上限，换来对目标的重创 + 尸爆 + 迟缓。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2GhostRing : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Damage = 伤害（25，升级 +7 → 32）；Slow = 迟缓层数（0，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(25m, ValueProp.Move),
        new DynamicVar("Slow", 0m)
    ];

    public lihuowang2GhostRing() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 生命上限 ≤5 时无法打出（原版保护）
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.MaxHp > 5;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;
        Creature target = cardPlay.Target!;

        // 1. 失去 5 点最大生命（当前生命同步钳制，避免溢出）
        int newMax = self.MaxHp - 5;
        self.MaxHp = newMax;
        if (self.CurrentHp > newMax)
            self.CurrentHp = newMax;

        // 2. 尸爆：敌人死亡时对其余敌人造成其最大生命的伤害
        await PowerCmd.Apply<Lihuowang2CorpseExplosionPower>(choiceContext, target, 1m, self, this);

        // 3. 迟缓（升级后才有效果）
        decimal slow = DynamicVars["Slow"].BaseValue;
        if (slow > 0m)
            await PowerCmd.Apply<SlowPower>(choiceContext, target, slow, self, this);

        // 4. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);
    }

    // 升级：伤害 25 → 32；迟缓 0 → 1
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7);
        DynamicVars["Slow"].UpgradeValueBy(1);
    }
}
