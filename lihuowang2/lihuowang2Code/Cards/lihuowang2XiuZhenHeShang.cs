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

// 修真·和尚：2 费技能（升级后 1 费），罕见，消耗。
// 失去 1 点最大能量，召唤随从「和尚」（6 生命、站在玩家后方、不替你承伤）。
// 那 1 点最大能量由随从身上的「先天一炁」扣着：随从死亡时能力消失，能量上限自动还回来。
//
// 唯一单位：场上已经有一只活着的和尚时，本牌不再召唤第二只，而是给已有的那只加血
// （+MinionHp 最大生命并回复等量生命，见 HeShang.ReinforceIfExisting）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XiuZhenHeShang : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // ===== 可调参数：召唤出来的和尚有多少血 =====
    // 作为召唤参数传给随从（MinionSummonOptions.MaxHp），同时：
    //   · 卡面文字里用动态变量 {MinionHp} 显示，改这里卡面会跟着变，不用手动改本地化表；
    //   · 随从 hover 的说明同样用 {MinionHp}（见 MinionHoverDescription）；
    //   · 「唯一」重复召唤时的加血量也用它（见 OnPlay）。
    // 想改成别的血量，只改这一个数字即可。（随从侧另有一个兜底默认值，见 HeShang.DefaultSummonHp。）
    private const int MinionHp = 6;

    // 卡面数值：MinionHp 只用于文本显示与上面的那几个用途，不随升级变化。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("MinionHp", MinionHp)
    ];

    // 本牌在模组本地化表 cards 里的键前缀（与 localization/<lang>/cards.json 一一对应）。
    // 引擎对卡牌标题/描述也是按 "<Id.Entry>.title/.description" 从 cards 表取的，
    // 这里沿用同一套规则，多出的 .minion.* 给随从 hover 用。
    private const string LocKeyPrefix = "LIHUOWANG2_CARD_LIHUOWANG2_XIU_ZHEN_HE_SHANG";

    // 修真 tag（供以后"修真"系的检索/联动用；纯机制标记，不显示在卡面，也不需要本地化文案）
    protected override HashSet<CardTag> CanonicalTags => [
        XiuZhenTags.XiuZhen
    ];

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    // 悬停提示：
    //  1. 随从「和尚」本身——卡面只写了召唤与最大能量的代价，这里补上它的血量与「唯一」规则
    //     （标题/描述走模组 cards 本地化表，图标直接用随从图）
    //  2. 随从身上的「先天一炁」（最大能量的扣减与死亡归还）
    //  3. 随从身上的「唯一」（已在场时不额外召唤、改为加血）
    //  4. 随从身上的「诵经」（召唤自带 1 层，攒到 10 层就说「世尊」+ 对随机敌人砸 999 点，然后回到 1 层）
    //  5. 随从身上的「行善」（每回合开始时随机给一个**友方角色**解一层负面状态，含双人模式的队友；不解随从）
    //     —— 卡面完全没提这两条，靠 hover 说明；说明文本走 powers 本地化表
    //     （LIHUOWANG2_POWER_SONG_JING_POWER.* / LIHUOWANG2_POWER_XING_SHAN_POWER.*）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new HoverTip(
            new LocString("cards", $"{LocKeyPrefix}.minion.title"),
            MinionHoverDescription(),
            GD.Load<Texture2D>(HeShang.SpritePath)),
        HoverTipFactory.FromPower<XiuZhenPower>(),
        HoverTipFactory.FromPower<UniquePower>(),
        HoverTipFactory.FromPower<SongJingPower>(),
        HoverTipFactory.FromPower<XingShanPower>()
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

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2XiuZhenHeShang.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2XiuZhenHeShang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 唯一单位：已经有活着的和尚时，改为给那一只加血（+MinionHp 最大生命并回复等量生命），
        // 不再召唤第二只。此时也不会再上「先天一炁」，所以重复打出不会重复扣最大能量。
        if (await HeShang.ReinforceIfExisting(Owner, MinionHp))
        {
            // 那只和尚的「诵经」也趁机 +1 层（到 10 层就照常砸 999 点）。
            await SongJingPower.AdvanceExisting(choiceContext, Owner, this);
            return;
        }

        // 召唤随从「和尚」，站在玩家**后方**（Back）。
        // "比秋吃饱更贴主角一点"的那点偏移由随从侧自己的摆位负责（见 HeShangLayout.CloserDistance）。
        // 随从没有挂守护（MinionGuardianPower），所以它不会替玩家承挡未格挡的伤害
        //（承不承伤只取决于有没有挂守护，与站前站后无关）。
        // 血量由本牌的 MinionHp 通过 MaxHp 参数传入（随从侧在 OnSummon 里落地，见 HeShang.OnSummon）。
        // 「失去 1 点最大能量」不需要在这里写：随从召唤时会上「先天一炁」1 层，
        // 那个能力负责 -1，并在随从死亡时随能力一起失效（= 归还）。
        await MinionCmd.AddMinion<HeShang>(choiceContext, Owner, new MinionSummonOptions(
            MaxHp: MinionHp,
            Source: this,
            Position: MinionPosition.Back));
    }

    // 升级：费用 2 → 1
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
