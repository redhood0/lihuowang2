using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using lihuowang2.Minions;
using lihuowang2.Powers;
using MinionLib.Commands;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 修真·秋吃饱：1 费技能（升级后 0 费），消耗。
// 失去 1 点最大能量，召唤随从「秋吃饱」（1 生命、站在玩家后方、不替你承伤）。
// 那 1 点最大能量由随从身上的「先天一炁」能力扣着：随从死亡时能力消失，能量上限自动还回来。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XiuZhenQiuChiBao : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 本牌在模组本地化表 cards 里的键前缀（与 localization/<lang>/cards.json 一一对应）。
    // 引擎对卡牌标题/描述也是按 "<Id.Entry>.title/.description" 从 cards 表取的，
    // 这里沿用同一套规则，多出的 .minion.* 给随从 hover 用。
    private const string LocKeyPrefix = "LIHUOWANG2_CARD_LIHUOWANG2_XIU_ZHEN_QIU_CHI_BAO";

    // 关键字：消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    // 悬停提示：
    //  1. 随从「秋吃饱」本身——卡面只写了召唤与最大能量的代价，这里补上它每回合的产石效果
    //     （标题/描述走模组 cards 本地化表，图标直接用随从图）
    //  2. 随从身上的「先天一炁」（最大能量的扣减与死亡归还）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new HoverTip(
            new LocString("cards", $"{LocKeyPrefix}.minion.title"),
            new LocString("cards", $"{LocKeyPrefix}.minion.description"),
            GD.Load<Texture2D>(QiuChiBao.SpritePath)),
        HoverTipFactory.FromPower<XiuZhenPower>()
    ];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2XiuZhenQiuChiBao.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2XiuZhenQiuChiBao() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 召唤随从「秋吃饱」，站在玩家后方（Back）。
        // 血量由 QiuChiBao 自己的 MinInitialHp / MaxInitialHp 决定（1）。
        // 「失去 1 点最大能量」不需要在这里写：随从召唤时会上「修真」，
        // 那个能力负责 -1，并在随从死亡时随能力一起失效（= 归还）。
        await MinionCmd.AddMinion<QiuChiBao>(choiceContext, Owner, new MinionSummonOptions(
            Source: this,
            Position: MinionPosition.Back));
    }

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
