using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2FireDefense : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 2;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 格挡牌（UI 会据此识别）
    public override bool GainsBlock => true;

    // 悬停提示：神火护体效果。
    // 传入当前点燃层数：能力面板的文案里有 {Amount}，不传的话会显示「0 层」。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<FireDefensePower>(DynamicVars["Ignite"].IntValue)];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2FireDefense.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：
    // Block  = 格挡（14，升级 +6 → 20）；
    // Ignite = 神火护体每次点燃的层数（2，升级 +1 → 3）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(14m, ValueProp.Move),
        new DynamicVar("Ignite", 2m)
    ];

    public lihuowang2FireDefense() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：获得格挡，并获得神火护体。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 获得格挡
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 2. 获得神火护体（本回合受击时点燃所有敌人，层数见 Ignite；下个自己回合开始时消失）。
        //    先移除旧实例再施加：这个能力不吃叠加，层数始终等于「本次打出」的数值。
        //    （否则同一回合打出第二张同名牌会走引擎的 ModifyAmount 累加，变成点燃 4/5 层。）
        FireDefensePower? existing = Owner.Creature.GetPower<FireDefensePower>();
        if (existing != null)
            await PowerCmd.Remove(existing);

        await PowerCmd.Apply<FireDefensePower>(choiceContext, Owner.Creature,
            DynamicVars["Ignite"].BaseValue, Owner.Creature, this);
    }

    // 升级：格挡 14 → 20；点燃层数 2 → 3
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(6);
        DynamicVars["Ignite"].UpgradeValueBy(1);
    }
}
