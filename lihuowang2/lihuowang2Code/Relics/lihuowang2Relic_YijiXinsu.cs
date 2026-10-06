using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 一炁·心素：初始遗物「心素」的**先祖**版本（RelicRarity.Ancient）。
//
// 身份规则：它**也视为心素** —— 携带它时，所有"需要心素"的牌（目前是 替身人皮 / lihuowang2XinSuSkin）
// 照样可以打出。判定统一走 lihuowang2Relic_Xinsu.HasXinsu(player)（那边把两张遗物都算心素）。
//
// 获取途径（未改动）：官方遗物「欧洛巴斯之触」（TouchOfOrobas）把初始遗物「心素」精炼成它 ——
// 映射注册在 lihuowang2Relic_Xinsu 上的 [RegisterTouchOfOrobasRefinement]。
//
// 效果（本次重做）：
//   1. 每回合开始时，将 1 张「疑虑」加入手牌（与「心素」同款）；
//   2. 每当你抽到 3 张「疑虑」时，获得「疯癫」1 回合（与「心素」同款，跨回合累计、不跨战斗）；
//   3. 每回合可以主动打出 1 张诅咒牌，然后获得 1 点能量。
//      —— 潜规则：这么打出的诅咒牌会**被消耗**（由本遗物给它临时加上 [消耗] 关键字实现，不写进卡面）。
//
// ⚠ 多人安全：两个计数都走 [SavedProperty]（本局存档 + 战斗快照双通道），
//   断线重连的客户端能把计数恢复成和主机一致；否则"第几张疑虑触发疯癫""本回合诅咒额度用没用"
//   两端会不同 → 行为不一致甚至校验和分歧。判定条件全部是内容信息，不按机器判断。
//
// 图标：images/relics/lihuowang2Relic_YijiXinsu.png。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_YijiXinsu : ModRelicTemplate
{
    // ===== 数值 =====
    // 攒够多少张疑虑触发一次疯癫
    private const int DoubtsPerCrazy = 3;
    // 每回合允许主动打出的诅咒牌张数
    private const int CursesAllowedPerTurn = 1;
    // 每打出一张诅咒回复的能量
    private const int EnergyPerCurse = 1;

    // 本场战斗内累计抽到的疑虑数量，满 DoubtsPerCrazy 张后清零。跨回合累计，不跨战斗。
    private int _doubtDrawnCount;
    // 本回合已经打出的诅咒牌数量。
    private int _cursesPlayedThisTurn;

    // ===== 存档 / 战斗快照出口（多人重连、两端一致性）=====

    [SavedProperty]
    public int DoubtDrawnCount
    {
        get => _doubtDrawnCount;
        private set
        {
            _doubtDrawnCount = value;
            UpdateCounterDisplay();
        }
    }

    [SavedProperty]
    public int CursesPlayedThisTurn
    {
        get => _cursesPlayedThisTurn;
        private set => _cursesPlayedThisTurn = value;
    }

    // 本回合"打诅咒"的额度是否还没用掉
    private bool CurseChargeAvailable => CursesPlayedThisTurn < CursesAllowedPerTurn;

    // 先祖遗物：不进普通掉落池与商店（引擎按稀有度取，Ancient 天然不会出现）。
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 计数器：只在积累了疑虑后才显示，默认（0）不显示
    public override bool ShowCounter => DoubtDrawnCount > 0;

    // 计数器显示的数字 = 当前积累的疑虑数（0..DoubtsPerCrazy-1）
    public override int DisplayAmount => DoubtDrawnCount;

    // 描述里出现的「疑虑」「能量」给官方 hover。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Doubt>(),
        HoverTipFactory.ForEnergy(this)
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // ===== 效果 1：每回合开始给 1 张疑虑 + 重置诅咒额度 =====

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人：Hook 会把「每个玩家的回合开始」通知给场上所有遗物，先筛掉队友的回合。
        if (player != Owner)
        {
            await base.AfterPlayerTurnStartEarly(choiceContext, player);
            return;
        }

        // 新回合 → 重置"打诅咒"的额度
        CursesPlayedThisTurn = 0;

        // 给 1 张疑虑：直接放进手牌，不走抽牌钩子，所以下面手动计入一次
        await CardPileCmd.AddToCombatAndPreview<Doubt>(Owner.Creature, PileType.Hand, 1, Owner);
        await CountDoubtAndTryTriggerCrazy(choiceContext);

        await base.AfterPlayerTurnStartEarly(choiceContext, player);
    }

    // ===== 效果 2：抽到 3 张疑虑 → 疯癫 1 回合 =====

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        // 只统计真正抽到手的疑虑，而且必须是自己的（队友抽牌也会通知到本遗物）
        if (card is not Doubt || card.Owner != Owner)
            return;

        await CountDoubtAndTryTriggerCrazy(choiceContext);
    }

    // 累计 1 张疑虑，满 DoubtsPerCrazy 张时施加疯癫并清零
    private async Task CountDoubtAndTryTriggerCrazy(PlayerChoiceContext choiceContext)
    {
        DoubtDrawnCount++;

        if (DoubtDrawnCount < DoubtsPerCrazy)
            return;

        DoubtDrawnCount = 0;
        await PowerCmd.Apply<CrazyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
    }

    // ===== 效果 3：每回合可以主动打出 1 张诅咒 → 获得 1 点能量（该诅咒被消耗）=====

    // 本回合还"可打出"的诅咒：去掉 [不可打出]，并加上 [消耗]（打出后进消耗堆 —— 潜规则）。
    // 额度用完后不再改关键字 → 该诅咒恢复成不可打出（UI 会照常提示原因）。
    // 这是全局关键字贡献：只要遗物还在就生效，不需要额外清理（与「火袄/怜悯」同款写法）。
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (card.Owner != Owner)
            return false;
        if (card.Type != CardType.Curse)
            return false;
        if (!CurseChargeAvailable)
            return false;

        bool changed = keywords.Remove(CardKeyword.Unplayable);
        changed |= keywords.Add(CardKeyword.Exhaust);
        return changed;
    }

    // 额度用完的诅咒不能再打出（同「怜悯」的 ShouldPlay）。不影响其它牌，也不额外拦截自动打出，
    // 免得把别的卡的效果卡住。
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
        => card.Owner != Owner || card.Type != CardType.Curse || CurseChargeAvailable;

    // 打出诅咒：消耗额度 + 获得能量（这张诅咒会被 [消耗] 关键字送进消耗堆）
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = cardPlay.Card;

        // 引擎会把"所有玩家打出的牌"都通知给场上遗物，先筛掉队友打的牌
        if (card?.Owner != Owner || card.Type != CardType.Curse)
            return;

        CursesPlayedThisTurn++;
        Flash();
        await PlayerCmd.GainEnergy(EnergyPerCurse, Owner);
    }

    // ===== 战斗内计数清理 =====

    public override async Task BeforeCombatStart()
    {
        DoubtDrawnCount = 0;
        CursesPlayedThisTurn = 0;
        await base.BeforeCombatStart();
    }

    public override async Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        DoubtDrawnCount = 0;
        CursesPlayedThisTurn = 0;
        await base.AfterCombatEnd(room);
    }

    // 通知 UI 刷新计数器显示
    private void UpdateCounterDisplay()
        => InvokeDisplayAmountChanged();

    // 塔1彩蛋：进入最终胜利房间时播放「成仙」BGM
    // （与「心素」相同 —— 心素被精炼成本遗物后，这个彩蛋不会丢）
    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        if (room.GetType().Name.IndexOf("Victory", System.StringComparison.OrdinalIgnoreCase) >= 0)
            Lihuowang2MusicUtil.PlayVictoryMusic("chengxian.mp3");

        await base.AfterRoomEntered(room);
    }
}
