using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 岁岁公主（李岁·公主形态）：本角色的专属「先祖」卡（CardRarity.Ancient）。
// 效果：固有。获得 2 层「黑太岁」；[触手]牌造成的伤害与获得的格挡 +X。
//      打出后角色形象变成「黑太岁」形态（与打出「黑太岁」卡完全一致，见 OnPlay 第 3 步）。
//      不限次升级（maxUpgradeLevel = int.MaxValue），每升一次 X +1（见 BoostPerUpgrade）。
//      因为「火堆升级选项」的可选性 = 卡组里是否还有 IsUpgradable 的牌，
//      它不限次 ⇒ 只要它在卡组里，火堆的「升级」选项就不会消失。
//      能力层数就是 X：多张岁岁公主打出去会**相加**（X 越大越强，能力图标上直接显示总值）。
// 它不进随机奖励池（引擎在 CardFactory 里就把 Ancient / Event 稀有度排除了），
// 获得途径是官方遗物「古老牙齿」（ArchaicTooth）：拿到那颗牙时，
// 卡组里的初始卡「黑太岁」会被超越成这张牌（映射注册见 Lihuowang2Heitaisui 上的
// [RegisterArchaicToothTranscendence]）。
[RegisterCard(typeof(lihuowang2CardPool))]
// 尘封古籍（达弗事件里的先古遗物）候选：本角色的先古卡必须在这里登记一份。
// 否则 DustyTome.SetupForPlayer 的候选池 =「本角色先古卡 - 所有超越卡」= 空，
// 取 .Id 时直接空引用崩溃，表现为进达弗事件卡住/报 NullReferenceException。
// RitsuLib 的补丁会优先取这里登记的候选，从而绕开引擎那段会崩的逻辑
// （见 STS2RitsuLib.Relics.Patches.DustyTomeSetupForPlayerPatch）。
[RegisterDustyTomeCard(typeof(lihuowang2Character))]
public sealed class lihuowang2LisuiGongzhu : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Ancient;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 打出的「黑太岁」层数。
    // ⚠ 改这里要同步 cards.json 本牌描述里的「[blue]N[/blue]层[blue]黑太岁[/blue]」（手写数字）。
    private const decimal HeitaisuiStacks = 2m;

    // X（触手牌伤害/格挡加成）的基础值。
    // 描述里用的是占位符 {Boost:diff()}，改这个数会跟着变，不需要改本地化文案。
    private const decimal BaseBoost = 2m;

    // 每次升级 X 的增量（当前 +1：滚得快慢就改这一个数）。
    // 引擎语义：CardModel.UpgradeInternal → OnUpgrade() 每升一级都会调用一次，
    // 而 DynamicVar.UpgradeValueBy 是「BaseValue += addend」，所以多次升级会自然累加
    // （基础 2 → +1 后 3 → 4 → 5……），不需要自己按 CurrentUpgradeLevel 反算。
    private const decimal BoostPerUpgrade = 1m;

    // 可升级次数上限：int.MaxValue = 真正不限次。
    //   · 引擎默认是 1（只能升一次）；> 1 时卡名会自动显示成「岁岁公主+1 / +2 …」（引擎行为）；
    //   · IsUpgradable =「当前等级 < MaxUpgradeLevel」；
    //   · 火堆「升级」选项的可用性就是 `Owner.Deck.UpgradableCardCount != 0`
    //     （SmithRestSiteOption.IsEnabled），而该计数用的正是 IsUpgradable ——
    //     所以上限若是 5，等其它牌都升满、它也到 +5 之后，火堆的「升级」就会整体变灰。
    //     改成 int.MaxValue 后，只要它在卡组里，火堆就永远有牌可升（这就是"无限升级"）。
    // ⚠ 代价：每处火堆都能继续喂它（每次 +2 X），强度会滚得很快。
    private const int maxUpgradeLevel = int.MaxValue;

    // 固有：开局就在手上（先祖卡只有一张，靠固有保证能摸到）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    // 多次升级：每次 +BoostPerUpgrade
    public override int MaxUpgradeLevel => maxUpgradeLevel;

    // 悬停提示：描述里出现的「黑太岁」（触手牌是牌类概念，没有对应的模型可挂 hover）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<Lihuowang2HeitaisuiPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2LisuiGongzhu.png（缺失时用占位图）。
    // 先祖卡专用的边框 / 文本底 / 横幅交给引擎按稀有度处理，这里不覆盖。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Boost = 触手牌造成的伤害与获得的格挡加成（升级每次 +2，可以升多次）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Boost", BaseBoost)
    ];

    public lihuowang2LisuiGongzhu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：获得 2 层黑太岁 + 给「岁岁公主」能力叠 X 层（层数就是触手加成值），
    // 并把战斗形象换成「黑太岁」形态（与打出「黑太岁」卡完全一致）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;

        // 1. 黑太岁 2 层（可叠加的 Counter 能力，层数即每回合的抽/消耗次数）
        await PowerCmd.Apply<Lihuowang2HeitaisuiPower>(choiceContext, self, HeitaisuiStacks, self, this);

        // 2. 触手加成：能力层数 = X。Counter 能力每次施加都是「累加」，
        //    所以再打一张岁岁公主就是 +XX（多张叠加 —— 这是刻意要的效果）。
        await PowerCmd.Apply<Lihuowang2LisuiGongzhuPower>(choiceContext, self,
            DynamicVars["Boost"].BaseValue, self, this);

        // 3. 形象变化（与黑太岁卡同一句）：lhw_character.png → lhw_trans01.png。
        //    只影响本机战斗场景的那具身体；还原由 Lihuowang2HeitaisuiPower.AfterRemoved 负责
        //    （本牌保证施加了该能力，所以黑太岁层数耗尽 / 被移除时，形象会跟着一起变回去）。
        Lihuowang2VisualUtil.SetBodyTexture(self, Lihuowang2VisualUtil.HeitaisuiBodyPath);
    }

    // 升级（可多次）：X +2
    protected override void OnUpgrade()
    {
        DynamicVars["Boost"].UpgradeValueBy(BoostPerUpgrade);
    }
}
