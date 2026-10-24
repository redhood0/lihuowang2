using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using lihuowang2.Encounters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔3（Act3 = Glory）：大船。
//
// 流程：起始 →
//   ①接受交易：获得 250 金币 + 把一张「悔恨」(Regret) 加入牌组 → 结束页；
//   ②他必须死！！！：进入一场硬仗（随机 2 只精英：1 只来自 3 层、1 只来自 2 层）
//                     → 打赢回事件看收尾页 → 点「拾起报酬」→ 2 件随机稀有遗物 + 88 金币（奖励界面）。
//
// ⚠ 开战的事件必须 IsShared = true（引擎硬性约束，见 LongmenWaterBandits / ZhengdeTemple 注释）。
// 只在 **3 层（Act3 = Glory，Index == 2）** 出现：只注册 Glory。
[RegisterActEvent(typeof(Glory))]
public sealed class lihuowang2BigShip : lihuowang2EventTemplate
{
    // ===== 效果参数 =====
    private const decimal TradeGold = 250m;  // 接受交易的金币
    private const int VictoryGold = 88;      // 打赢后额外给的金币

    // 奖励是否已发过：防止「拾起报酬」被连点两次拿两份遗物。
    private bool _rewardTaken;

    // 开战要求：见类注释。
    public override bool IsShared => true;

    // 立绘暂时复用**官方事件图** `res://images/events/drowning_beacon.png`（官方「溺亡信标」，
    // 海上孤零零的庞然大物，正合"北海新港的白色游轮"这个画面）。
    // ⚠ 这里必须指向一个**真实存在**的图：指到不存在的文件会让事件节点初始化崩溃，
    //   表现就是打开全黑、选项也不显示（正德寺踩过这个坑）。
    // 换成自己的图：画一张放 `images/event/BigShip.png`，再把下面这行改成
    // `$"{Entry.ResPath}/images/event/BigShip.png"`。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://images/events/drowning_beacon.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, AcceptTrade, InitialOptionKey("ACCEPT")),
            new EventOption(this, HeMustDie, InitialOptionKey("FIGHT")),
        ];
    }

    // ①接受交易：250 金币 + 悔恨
    private async Task AcceptTrade()
    {
        Player owner = Owner!;
        await PlayerCmd.GainGold(TradeGold, owner);
        await ObtainCardToDeck<Regret>();
        SetEventFinished(PageDescription("TRADE_END"));
    }

    // ②他必须死！！！：开一场双精英的硬仗，打赢后 Resume 回来看收尾页
    private Task HeMustDie()
    {
        EnterCombatWithoutExitingEvent(
            ModelDb.Encounter<lihuowang2BigShipEncounter>(), [], shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    // 战斗结束回到事件：**只显示收尾页，不发奖励**。
    // 奖励挪到玩家点「拾起报酬」时才给（见 TakeReward），这样节奏是先看完剧情再拿东西。
    // 注意这里不判断胜负——战败时本局已经结束，走不到 Resume。
    public override Task Resume(AbstractRoom exitedRoom)
    {
        SetEventState(PageDescription("VICTORY"),
        [
            new EventOption(this, TakeReward, ModOptionKey("VICTORY", "TAKE")),
        ]);
        return Task.CompletedTask;
    }

    // 点「拾起报酬」：结束事件页 → 弹随机稀有遗物的奖励界面。
    // 写法对齐官方「历战假人」(BattlewornDummy)：SetEventFinished 之后才 OfferCustom，
    // 否则奖励界面会叠在还没关掉的事件页上。
    private async Task TakeReward()
    {
        // 防连点：SetEventFinished 之后还能再点一次这个选项，会把遗物发两份。
        if (_rewardTaken)
        {
            return;
        }

        _rewardTaken = true;
        Player owner = Owner!;
        SetEventFinished(PageDescription("VICTORY_END"));

        // 用 RelicReward 而不是直接 RelicCmd.Obtain，原因：
        //   · 玩家能看到奖励卡（图标、名称、悬停说明），点一下领取，和官方事件一致；
        //   · RelicReward.OnSelect 内部就是 `RelicCmd.Obtain`，所以遗物的「获取时」效果
        //     （AfterObtained：加最大生命、塞诅咒、变形卡牌……）照常触发；
        //   · Populate 走 RelicFactory.PullNextRelicFromFront(player, rarity)，
        //     自带「抓取袋不重复 / 联机一致 / 抽空回落 Circlet」，不用自己处理。
        // 两个 RelicReward 是各自 Populate 的，会从抓取袋里前后取两件不同的稀有遗物。
        await RewardsCmd.OfferCustom(owner,
        [
            new RelicReward(RelicRarity.Rare, owner),
            new RelicReward(RelicRarity.Rare, owner),
            new GoldReward(VictoryGold, owner),
        ]);
    }
}
