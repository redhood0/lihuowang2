using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using lihuowang2.Characters;
using lihuowang2.Tags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 一炁·心素：初始遗物「心素」的**先祖**版本（RelicRarity.Ancient）。
//
// 身份规则：它**也视为心素** —— 携带它时，所有"需要心素"的牌（目前是 替身人皮 / lihuowang2XinSuSkin）
// 照样可以打出。判定统一走 lihuowang2Relic_Xinsu.HasXinsu(player)（那边把两张遗物都算心素），
// 牌那边不要再自己判遗物类型，否则每加一张心素系遗物都要改一遍牌。
//
// 获取途径：官方遗物「欧洛巴斯之触」（TouchOfOrobas）会把初始遗物「心素」精炼成它 ——
// 映射注册在 lihuowang2Relic_Xinsu 上的 [RegisterTouchOfOrobasRefinement]，
// 与卡牌侧「黑太岁 → 岁岁公主」用 [RegisterArchaicToothTranscendence]（古老牙齿）完全对应。
// 它本身是先祖稀有度（RelicRarity.Ancient），不进普通掉落池与商店。
//
// 效果：
//   1. 带层数（初始 1 层，上限 7 层，图标上直接显示）；
//   2. 每场战斗开始时（= 本场第一次能量重置之后）：获得等同层数的能量；
//   3. 层数达到 3 / 5 / 7 层时，各额外 +1 点最大能量（累进：3 层 +1、5 层 +2、7 层 +3）；
//   4. 层数成长靠"打【修真】tag 的牌"：升到下一级分别需要累计打出 2 / 4 / 8 / 16 / 32 / 64 张
//      （逐级翻倍，见 RequirementsPerLevel），攒够就 +1 层并把进度清零；
//      进度在遗物说明里以「修真进度：x/y」显示（x = 已打出次数，y = 升级所需次数）。
// 图标：images/relics/lihuowang2Relic_YijiXinsu.png。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_YijiXinsu : ModRelicTemplate
{
    // ===== 数值（改这里就够，文案里的数字都走占位符跟着变）=====
    // 层数初始值 / 上限
    private const int InitialStacks = 1;
    private const int MaxStacks = 7;
    // 加最大能量的门槛（达到即生效，累进）：3 层 +1、5 层再 +1、7 层共 +3。
    // 想加减档位/改门槛只动这个数组；说明里的数字走 {Tier1}/{Tier2}/{Tier3}/{Tiers} 占位符跟着变。
    private static readonly int[] MaxEnergyTiers = [3, 5, 7];
    private const decimal MaxEnergyPerTier = 1m;

    // 1→2 级、2→3 级……6→7 级，各需要累计打出多少张【修真】牌（逐级翻倍）。
    // 与 MaxStacks 的对应关系：表里第 i 个数 = 从 i+1 级升到 i+2 级的需求，所以长度 = MaxStacks - 1。
    private static readonly int[] RequirementsPerLevel = [2, 4, 8, 16, 32, 64];

    private int _stacks = InitialStacks;
    private int _xiuZhenCount;

    // 层数：存档 + 多人战斗快照的出口（遗物是引擎里唯一支持 [SavedProperty] 的模型）。
    [SavedProperty]
    public int Stacks
    {
        get => _stacks;
        private set
        {
            _stacks = Math.Clamp(value, 0, MaxStacks);
            UpdateCounterDisplay();
        }
    }

    // 当前进度：距离下一级已经累计了多少张【修真】牌（满级时停在满格数字上，供说明显示 x/x）。
    [SavedProperty]
    public int XiuZhenCount
    {
        get => _xiuZhenCount;
        private set
        {
            _xiuZhenCount = Math.Clamp(value, 0, LastRequirement);
            UpdateCounterDisplay();
        }
    }

    // 表里最后一个需求（满级门槛），也是进度的上限值。
    private static int LastRequirement => RequirementsPerLevel[^1];

    // 距离下一级还需要的【修真】牌张数；已满级返回 0。
    public int RequiredForNextLevel => Stacks >= MaxStacks ? 0 : RequirementsPerLevel[Stacks - 1];

    // 先祖遗物（与卡牌的 CardRarity.Ancient 对应）：
    // 引擎的商店/掉落按稀有度取，Ancient 的 MerchantCost 是 999999999，天然不会出现在商店；
    // 普通宝箱/奖励也只会按 Common/Uncommon/Rare 等取（与「替身人皮」用 Event 不进普通池同理）。
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 计数器：始终显示层数（初始就有 1 层，不是"攒够了才显示"）
    public override bool ShowCounter => true;

    // 图标上显示的数字 = 层数（修真进度在遗物说明里显示，不放图标上）
    public override int DisplayAmount => Stacks;

    // 图片资源统一放在 AssetProfile 里配置（小图/轮廓图/大图先用同一张）。
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 描述里出现的「能量」给官方 hover。
    // ⚠ 用 AdditionalHoverTips 而不是 ExtraHoverTips：RitsuLib 的 ModRelicTemplate 把后者封了
    //   （模组遗物统一走 AdditionalHoverTips，参考 lihuowang2Relic_XiuMuRuYi）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.ForEnergy(this)];

    // 描述里的数字都用这些变量（引擎渲染遗物文本时会注入 DynamicVars）：
    // 改上面的常量/需求表，卡面/提示里的数字会跟着变，不需要动本地化文案。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("MaxStacks", MaxStacks),
        new IntVar("Tier1", MaxEnergyTiers[0]),
        new IntVar("Tier2", MaxEnergyTiers[1]),
        new IntVar("Tier3", MaxEnergyTiers[2]),
        new IntVar("TierEnergy", MaxEnergyPerTier),
        new TiersVar(),      // {Tiers}    -> "3/5/7"
        new RatesVar(),      // {Rates}    -> "2/4/8/16/32/64"
        new ProgressVar()    // {Progress} -> "3/8"（满级 "64/64"）
    ];

    // 最大能量：每达到一个门槛就 +1（累进：3 层 +1、5 层 +2、7 层 +3）。
    // ⚠ 必须判 player != Owner：引擎会为每个玩家问一遍场上所有遗物（官方给能量的遗物全都这么写），
    //   不筛就会给队友也加能量上限。
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner)
            return amount;

        decimal bonus = 0m;
        foreach (int tier in MaxEnergyTiers)
        {
            if (Stacks >= tier)
                bonus += MaxEnergyPerTier;
        }

        return amount + bonus;
    }

    // 每场战斗开始时（本场第一次能量重置之后）获得等同层数的能量。
    // 用 AfterEnergyReset 而不是 BeforeCombatStart：战斗内的能量会在回合开始被重置成最大能量，
    // 写在 BeforeCombatStart 给的能量会被那次重置冲掉（官方"开局给能量"的遗物也都是挂在这里）。
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner)
            return;

        // 只在本场第一次重置时给（TurnNumber 由引擎在本钩子之前指到当前回合）
        if (Owner.PlayerCombatState?.TurnNumber != 1)
            return;

        Flash();
        await PlayerCmd.GainEnergy(Stacks, Owner);
    }

    // 层数成长：打出带【修真】tag 的牌就累计进度，攒够需求升 1 层。
    // 用 AfterCardPlayed 而不是"按具体卡类判"：以后新增任何修真牌都会自动计入（只要挂上 tag）。
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = cardPlay.Card;

        // ⚠ 引擎会把"所有玩家打出的牌"都通知给场上遗物，所以必须筛掉队友打的牌
        //   （与「心素」AfterCardDrawn 里 card.Owner != Owner 的写法一致）。
        if (card?.Owner != Owner || !card.Tags.Contains(XiuZhenTags.XiuZhen))
            return;

        int required = RequiredForNextLevel;
        if (required <= 0)
        {
            // 已满级：进度停在满格（说明里显示 64/64），不再累计
            XiuZhenCount = LastRequirement;
            return;
        }

        XiuZhenCount++;
        if (XiuZhenCount < required)
            return;

        // 攒够 → 升 1 层；进度清零（满级时改成停在满格数字，方便显示 x/x）
        Stacks++;
        XiuZhenCount = Stacks >= MaxStacks ? LastRequirement : 0;
        Flash();
    }

    // 计数器显示刷新（RelicModel.InvokeDisplayAmountChanged）
    private void UpdateCounterDisplay()
        => InvokeDisplayAmountChanged();

    // 「3/5/7」这一串门槛数字：从 MaxEnergyTiers 生成，改门槛就跟着变
    // （想用整串数字写说明就引用 {Tiers}；想分开写就用 {Tier1}/{Tier2}/{Tier3}）。
    private sealed class TiersVar : DynamicVar
    {
        public TiersVar() : base("Tiers", 0m) { }

        public override string ToString()
            => string.Join("/", MaxEnergyTiers);
    }

    // 「每 2/4/8/16/32/64 张」那一串数字：直接从需求表生成，改表就跟着变。
    private sealed class RatesVar : DynamicVar
    {
        public RatesVar() : base("Rates", 0m) { }

        public override string ToString()
            => string.Join("/", RequirementsPerLevel);
    }

    // 「修真进度：x/y」的 x/y。引擎渲染遗物文本读的就是 DynamicVar.ToString()
    // （官方 StringVar / 模组 Dice18 的 RelicTextVar 都是这么供值的），所以这里直接拼字符串，
    // 本地化里写 {Progress} 占位即可 —— 换语言不用改文案，数字永远和代码同步。
    private sealed class ProgressVar : DynamicVar
    {
        public ProgressVar() : base("Progress", 0m) { }

        public override string ToString()
        {
            if (_owner is not lihuowang2Relic_YijiXinsu relic)
                return string.Empty;

            int required = relic.RequiredForNextLevel;
            // 满级：显示"已完成"的满格进度（64/64），而不是 0/0
            return required <= 0
                ? $"{LastRequirement}/{LastRequirement}"
                : $"{relic.XiuZhenCount}/{required}";
        }
    }
}
