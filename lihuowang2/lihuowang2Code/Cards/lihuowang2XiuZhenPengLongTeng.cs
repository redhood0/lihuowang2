using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Minions;
using lihuowang2.Powers;
using lihuowang2.Tags;
using MinionLib.Commands;
using MinionLib.Minion;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 修真·彭龙腾：2 费技能（升级后 1 费），稀有，消耗。
// 失去 1 点最大能量，召唤随从「彭龙腾」（20 生命、站在玩家前方、替玩家承受未格挡的伤害）。
// 那 1 点最大能量由随从身上的「先天一炁」扣着：随从死亡时能力消失，能量上限自动还回来。
//
// 唯一单位：场上已经有一只活着的彭龙腾时，本牌不再召唤第二只，而是给已有的那只加血
// （+MinionHp 最大生命并回复等量生命，见 PengLongTeng.ReinforceIfExisting）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XiuZhenPengLongTeng : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // ===== 可调参数：召唤出来的彭龙腾有多少血 =====
    // 作为召唤参数传给随从（MinionSummonOptions.MaxHp），同时：
    //   · 卡面文字里用动态变量 {MinionHp} 显示，改这里卡面会跟着变，不用手动改本地化表；
    //   · 随从 hover 的说明同样用 {MinionHp}（见 MinionHoverDescription）；
    //   · 「唯一」重复召唤时的加血量也用它（见 OnPlay）。
    // 想改成别的血量，只改这一个数字即可。（随从侧另有一个兜底默认值，见 PengLongTeng.DefaultSummonHp。）
    private const int MinionHp = 21;

    // 卡面数值：MinionHp 只用于文本显示与上面的那几个用途，不随升级变化。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("MinionHp", MinionHp)
    ];

    // 本牌在模组本地化表 cards 里的键前缀（与 localization/<lang>/cards.json 一一对应）。
    // 引擎对卡牌标题/描述也是按 "<Id.Entry>.title/.description" 从 cards 表取的，
    // 这里沿用同一套规则，多出的 .minion.* 给随从 hover 用。
    private const string LocKeyPrefix = "LIHUOWANG2_CARD_LIHUOWANG2_XIU_ZHEN_PENG_LONG_TENG";

    // 修真 tag（供以后"修真"系的检索/联动用；纯机制标记，不显示在卡面，也不需要本地化文案）
    protected override HashSet<CardTag> CanonicalTags => [
        XiuZhenTags.XiuZhen
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    // 悬停提示：
    //  1. 随从「彭龙腾」本身——卡面只写了召唤与最大能量的代价，这里补上它是「守护」单位
    //     （标题/描述走模组 cards 本地化表，图标直接用随从图）
    //  2. 随从身上的「先天一炁」（最大能量的扣减与死亡归还）
    //  3. 随从身上的「将相首」（每回合开始时随机给 1 张战士牌并本回合 0 费）
    //     —— 卡面完全没提这条效果，靠 hover 说明；
    //     说明文本走 powers 本地化表（LIHUOWANG2_POWER_JIANG_XIANG_SHOU_POWER.*）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new HoverTip(
            new LocString("cards", $"{LocKeyPrefix}.minion.title"),
            MinionHoverDescription(),
            GD.Load<Texture2D>(PengLongTeng.SpritePath)),
        HoverTipFactory.FromPower<XiuZhenPower>(),
        HoverTipFactory.FromPower<JiangXiangShouPower>()
    ];

    // 随从 hover 的说明里带 {MinionHp}。
    // 引擎只会把 DynamicVars 自动注入卡牌自己的 title/description，这条是我们手动 new 出来的 LocString，
    // 所以要自己把变量塞进去 —— 不塞的话卡面上会原样显示 "{MinionHp}"。
    private static LocString MinionHoverDescription()
    {
        LocString loc = new("cards", $"{LocKeyPrefix}.minion.description");
        loc.Add("MinionHp", MinionHp);
        return loc;
    }

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2XiuZhenPengLongTeng.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2XiuZhenPengLongTeng() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 唯一单位：已经有活着的彭龙腾时，改为给那一只加血（+MinionHp 最大生命并回复等量生命），
        // 不再召唤第二只。此时也不会再上「先天一炁」，所以重复打出不会重复扣最大能量。
        if (await PengLongTeng.ReinforceIfExisting(Owner, MinionHp))
            return;

        // 召唤随从「彭龙腾」，站在玩家前方（Front）。
        // Front 不只是站位：守护（MinionGuardianPower）内部要求 Position == Front 才启用，
        // 所以这个参数就是「替你承伤」的开关，别改成 Back。
        // 血量由本牌的 MinionHp 通过 MaxHp 参数传入（随从侧在 OnSummon 里落地，见 PengLongTeng.OnSummon）。
        // 「失去 1 点最大能量」不需要在这里写：随从召唤时会上「先天一炁」1 层，
        // 那个能力负责 -1，并在随从死亡时随能力一起失效（= 归还）。
        await MinionCmd.AddMinion<PengLongTeng>(choiceContext, Owner, new MinionSummonOptions(
            MaxHp: MinionHp,
            Source: this,
            Position: MinionPosition.Front));
    }

    // 升级：费用 2 → 1
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
