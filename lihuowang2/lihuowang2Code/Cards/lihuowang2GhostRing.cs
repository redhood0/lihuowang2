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
using lihuowang2.Powers;   // 丹阳子化（Lihuowang2DanyangziPower）；尸爆 power 仍停用（见 Lihuowang2CorpseExplosionPower.cs）
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 幽灵环（游老爷）：自损 5 点生命上限，换来对目标的重创 + 迟缓。
// （原「尸爆」效果暂时停用，power 代码保留在 Lihuowang2CorpseExplosionPower.cs 的注释块里。）
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

    // 悬停提示：描述里出现的「迟缓」。
    // 迟缓是官方能力（SlowPower），文案直接取官方 powers 表，不需要自己写；
    // 和「触手·缠绕」给官方 StranglePower 挂 hover 的做法一致。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<SlowPower>()];

    // Damage = 伤害（25，升级 +7 → 32）；Slow = 迟缓层数（1，升级 +1 → 2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(25m, ValueProp.Move),
        new DynamicVar("Slow", 1m)
    ];

    public lihuowang2GhostRing() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 丹阳子化生效中：本牌不再扣生命上限（见 OnPlay），所以"上限太低打不出"那条保护也就不需要了
    private bool HasDanyangzi => Owner?.Creature.GetPower<Lihuowang2DanyangziPower>() != null;

    // 生命上限 ≤5 时无法打出（原版保护；丹阳子化时不扣上限，所以不拦）
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true; // 图鉴/预览场景不做限制
            if (HasDanyangzi)
                return true;
            return Owner.Creature.MaxHp > 5;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;
        Creature target = cardPlay.Target!;

        // 1. 失去 5 点最大生命（当前生命同步钳制，避免溢出）
        //    丹阳子化期间不扣上限：「游老爷」的新增效果（卡面最后一行有说明）。
        if (!HasDanyangzi)
        {
            int newMax = self.MaxHp - 5;
            self.MaxHp = newMax;
            if (self.CurrentHp > newMax)
                self.CurrentHp = newMax;
        }

        // 2. 尸爆：暂时停用（power 本体见 Lihuowang2CorpseExplosionPower.cs，已整块注释，后面要用再一起放开）
        // await PowerCmd.Apply<Lihuowang2CorpseExplosionPower>(choiceContext, target, 1m, self, this);

        // 3. 迟缓：基础 1 层，升级后 2 层（与卡面说明一致）
        await PowerCmd.Apply<SlowPower>(choiceContext, target, DynamicVars["Slow"].BaseValue, self, this);

        // 4. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_gaze")   // 命中特效：凝视（游老爷的阴森一眼）
            .Execute(choiceContext);
    }

    // 升级：伤害 25 → 32；迟缓 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7);
        DynamicVars["Slow"].UpgradeValueBy(1);
    }
}
