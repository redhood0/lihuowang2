using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔3（Act3 = Glory）：上极灌口。
//
// 流程：起始 →
//   ①交换秘密：从牌组里选一张牌当**祭品**（选定后从牌组移除），再按祭品的稀有度/类型分三支：
//        · 白(Common) / 诅咒(Curse) / 状态(Status) / 任务(Quest) → 毫无反应，事件结束（无奖励）
//        · 蓝(Uncommon) / 事件(Event) → 裂隙愈合一小部分 → 二选一：随机**普通**遗物 / 回复 15 生命
//        · 金(Rare) / 先古(Ancient)  → 裂隙飞快愈合     → 二选一：随机**稀有**遗物 / 回复 30 生命
//   ②离开：直接结束
//
// 只在 **3 层（Act3 = Glory，Index == 2）** 出现：只注册 Glory。
[RegisterActEvent(typeof(Glory))]
public sealed class lihuowang2ShangjiGuankou : lihuowang2EventTemplate
{
    // ===== 效果参数（都集中在这里）=====
    private const decimal PartialHealAmount = 15m; // 蓝/事件 档的回血量
    private const decimal FullHealAmount = 30m;    // 金/先古 档的回血量

    // 祭品档位
    private enum Offering
    {
        Worthless, // 白 / 诅咒 / 状态 / 任务：毫无反应
        Partial,   // 蓝 / 事件：裂隙愈合一小部分
        Full,      // 金 / 先古：裂隙飞快愈合
    }

    // 流程状态：用来防止"一次献祭被重复触发"，见 ExchangeSecret 与 TakeCommonRelic 的注释。
    private Offering? _offeringTier; // null = 还没献祭；非空 = 已献祭，并记下档位
    private bool _rewardTaken;       // 奖励是否已领过（防连点拿两份）

    // 立绘暂时复用**官方事件图** `res://images/events/reflections.png`（官方「倒影」，
    // 正合本事件"水中倒影"这个核心意象）。
    // 换成自己的图：画一张放 `images/event/ShangjiGuankou.png`，再把下面这行改成
    // `$"{Entry.ResPath}/images/event/ShangjiGuankou.png"`（背景走引擎默认事件背景）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/ShangjiGuankou.png");

    // 事件变量：只有回血量一项（HealVar 的变量名是 "Heal"，文案里写 {Heal}）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(0m)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, ExchangeSecret, InitialOptionKey("EXCHANGE")),
            new EventOption(this, Leave, InitialOptionKey("LEAVE")),
        ];
    }

    // ①交换秘密：选一张牌当祭品 → 从牌组移除 → 按祭品档位分支
    private async Task ExchangeSecret()
    {
        // ⚠ 已经献过祭了：再点这个选项只重回奖励页，**绝不再开一次选牌界面 / 再献一张牌**。
        //   正常情况这个选项会被 DisableOnChosen 禁用；这里兜住"页面切换还没生效就被再点一次"。
        if (_offeringTier.HasValue)
        {
            ShowResultPage(_offeringTier.Value);
            return;
        }

        Player owner = Owner!;

        // 选牌界面：RequireManualConfirmation 让"只剩 1 张"时也强制玩家确认，免得误献；
        // Cancelable 允许取消（牌组里没有可移除的牌时也走这条），取消就回起始页重选或离开。
        // 注意：RequireManualConfirmation / Cancelable 是 init-only，只能在对象初始值设定项里赋值。
        CardSelectorPrefs prefs = new CardSelectorPrefs(SelectPrompt, 1)
        {
            RequireManualConfirmation = true,
            Cancelable = true,
        };

        CardModel? offering = (await CardSelectCmd.FromDeckForRemoval(owner, prefs)).FirstOrDefault();
        if (offering == null)
        {
            SetEventState(PageDescription("INITIAL"), GenerateInitialOptions());
            return;
        }

        Offering tier = ClassifyOffering(offering);
        await CardPileCmd.RemoveFromDeck(offering);
        _offeringTier = tier;
        ShowResultPage(tier);
    }

    // 按祭品档位显示结果页（献祭完成后调用；上面"重复点击"的兜底也走这里，保证回到同一页）
    private void ShowResultPage(Offering tier)
    {
        switch (tier)
        {
            case Offering.Partial:
                DynamicVars.Heal.BaseValue = PartialHealAmount;
                SetEventState(PageDescription("PARTIAL"),
                [
                    new EventOption(this, TakeCommonRelic, ModOptionKey("PARTIAL", "RELIC")),
                    new EventOption(this, HealAndFinish, ModOptionKey("PARTIAL", "HEAL")),
                ]);
                break;

            case Offering.Full:
                DynamicVars.Heal.BaseValue = FullHealAmount;
                SetEventState(PageDescription("FULL"),
                [
                    new EventOption(this, TakeRareRelic, ModOptionKey("FULL", "RELIC")),
                    new EventOption(this, HealAndFinish, ModOptionKey("FULL", "HEAL")),
                ]);
                break;

            default:
                SetEventFinished(PageDescription("NO_RESPONSE"));
                break;
        }
    }

    // 祭品档位判定：**类型优先**（诅咒/状态/任务 不管稀有度都算无用），其余按稀有度。
    // 白 = Common、蓝 = Uncommon、金 = Rare、先古 = Ancient、事件 = Event。
    // 没提到的 Basic（初始打击/防御）与 Token 一并归入"无用"档。
    private static Offering ClassifyOffering(CardModel card)
    {
        if (card.Type is CardType.Curse or CardType.Status or CardType.Quest)
        {
            return Offering.Worthless;
        }

        return card.Rarity switch
        {
            CardRarity.Rare or CardRarity.Ancient => Offering.Full,
            CardRarity.Uncommon or CardRarity.Event => Offering.Partial,
            _ => Offering.Worthless,
        };
    }

    // 蓝/事件 档：随机普通遗物
    private async Task TakeCommonRelic()
    {
        // ⚠ 必须先上锁：下面 ObtainRandomRelic / CreatureCmd.Heal 都是 await，
        //   而引擎的 DisableOnChosen 只禁用**被点这一个**，另一个奖励选项在这段窗口里仍是活的，
        //   连点就会既拿到遗物又回血，还会 SetEventFinished 两次。
        if (_rewardTaken)
        {
            return;
        }

        _rewardTaken = true;
        await ObtainRandomRelic(RelicRarity.Common);
        SetEventFinished(PageDescription("RELIC_END"));
    }

    // 金/先古 档：随机稀有遗物
    private async Task TakeRareRelic()
    {
        if (_rewardTaken)
        {
            return;
        }

        _rewardTaken = true;
        await ObtainRandomRelic(RelicRarity.Rare);
        SetEventFinished(PageDescription("RELIC_END"));
    }

    // 取一件指定稀有度的随机遗物。
    // 走玩家的**遗物抓取袋**（官方 RelicFactory.PullNextRelicFromFront 内部就是调它），
    // 而不是自己 Random，因为：
    //   · 抓取袋保证这件遗物本局不会再出现（不会重复给）；
    //   · 各端取的是同一个袋子，联机一致；
    //   · 取不到（该稀有度已抽空）时回落到官方兜底遗物 Circlet，不会返回 null。
    // 取完还要从共享抓取袋里移除，免得别的玩家/宝箱再抽到同一件（与官方一致）。
    private async Task ObtainRandomRelic(RelicRarity rarity)
    {
        Player owner = Owner!;
        RelicModel relic = owner.RelicGrabBag.PullFromFront(rarity, owner.RunState) ?? ModelDb.Relic<Circlet>();
        owner.RunState.SharedRelicGrabBag.Remove(relic);
        await RelicCmd.Obtain(relic.ToMutable(), owner);
    }

    // 回复 {Heal} 生命（值在进入该分支时已按档位设好）
    private async Task HealAndFinish()
    {
        if (_rewardTaken)
        {
            return;
        }

        _rewardTaken = true;
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.BaseValue);
        SetEventFinished(PageDescription("HEAL_END"));
    }

    // ②离开
    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE_END"));
        return Task.CompletedTask;
    }

    // 选牌界面的提示语：自己加的本地化键，见 localization/*/events.json。
    // 想省事可以直接换成官方的 CardSelectorPrefs.RemoveSelectionPrompt。
    private static LocString SelectPrompt =>
        new LocString("events", "LIHUOWANG2_EVENT_LIHUOWANG2_SHANGJI_GUANKOU.selectPrompt");
}
