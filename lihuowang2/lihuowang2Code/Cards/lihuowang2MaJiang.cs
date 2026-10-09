using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MaJiang : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Draw = 抽牌数（2，升级 +1 → 3）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Draw", 2m)
    ];

    public lihuowang2MaJiang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 造成固定 3、2、1 点伤害各一次（合计 6 点），然后抽牌。
    //
    // .Unpowered() = 这三下都是"固定伤害"，不吃任何伤害修正：
    //   · 力量 / 力量削减（StrengthPower 的 ModifyDamageAdditive）
    //   · 易伤 1.5 倍 / 虚弱 0.75 倍（VulnerablePower / WeakPower 的 ModifyDamageMultiplicative）
    //   · 纸蛙之类的"增伤遗物"
    //   这些都是先判断 props.IsPoweredAttack()，而 Unpowered 会让它返回 false —— 一律跳过。
    //   引擎自己的常量就是 DamageProps.cardUnpowered = Unpowered | Move，官方 Omnislice 的溅射伤害也是这么写的。
    //
    // 注意：只加了 Unpowered，没有加 Unblockable —— 所以**护甲照常挡**（Unblockable 是中毒那种直接掉血）。
    // 另外因为不再是"带力量的攻击"，敌人的尖刺（ThornsPower 也判断 IsPoweredAttack）不会再反伤 —— 与官方
    // Omnislice 同款行为（引擎里专门给它开了个例外）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        for (decimal dmg = 3m; dmg >= 1m; dmg -= 1m)
        {
            await DamageCmd.Attack(dmg)
                .FromCard(this, cardPlay)
                .Unpowered()                          // 固定伤害：跳过力量/易伤/虚弱等一切伤害修正
                .Targeting(cardPlay.Target!)
                .WithHitFx("vfx/vfx_attack_blunt")   // 命中特效：钝击（掷出的牌砸人）
                .Execute(choiceContext);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, Owner.Creature.Player!);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1);
    }
}
