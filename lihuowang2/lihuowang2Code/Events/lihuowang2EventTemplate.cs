using System.Linq;
using System.Threading.Tasks;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 文本类事件通用模板：复刻塔1 PhasedEvent 的“页面→选项→奖励→收尾”写法。
// 事件没有立绘/背景时请勿把 AssetProfile 返回 Empty（引擎仍会尝试加载默认事件图），
// 所以三个事件都配了 InitialPortraitPath。
public abstract class lihuowang2EventTemplate : ModEventTemplate
{
    // 最早允许出现的层号（ActFloor = 地图坐标 row + 1，**row 0 的起始 Ancient/Neow 点就是第 1 层**）。
    // 默认 1 = 不限制；子类覆写它就能让事件"前面几个点不出现"。
    // ⚠ ActFloor 每个 act 各自从 1 开始算，不是整局累计（要累计得用 TotalFloor，但它不含当前点）。
    protected virtual int MinActFloor => 1;

    // 事件出现条件：队伍里必须有人玩「李火旺」，且不在前几个点。
    // 引擎的勾子（EventModel.IsAllowed，虚方法，默认 true）：ActModel.PullNextEvent →
    // RoomSet.EnsureNextEventIsValid 每次抽事件都会问一遍，返回 false 就跳过该事件继续抽，
    // 所以它既不进事件池的输出、也不会把池子卡住（与官方 RelicTrader 判“5 件遗物 + 第二幕起”同一套写法）。
    // 注意：注册阶段（ActModel.GenerateRooms）只按 Epoch 过滤，不看 IsAllowed，条件只能写在这里。
    // 时机：RunManager.EnterMapPointInternal 先 `State.ActFloor = actFloor`、再 CreateRoom → PullNextEvent，
    //       所以这里读到的 ActFloor 就是"即将进入的这个点"的层号（TotalFloor/MapPointHistory 则不含当前点）。
    // 多人时 Players 是全体玩家，Any 即“任意一名玩家选了李火旺”。
    public override bool IsAllowed(IRunState runState)
        => runState.ActFloor >= MinActFloor
           && runState.Players.Any(p => p != null && p.Character is lihuowang2Character);

    // 把一张牌永久加入卡组，并弹出获得预览（对应塔1 ShowCardAndObtainEffect）。
    // 必须用 RunState.CreateCard 生成带归属的实例，否则 CardPileCmd.Add 会报 “has no owner”。
    protected async Task ObtainCardToDeck<T>() where T : CardModel
    {
        Player owner = Owner!;
        CardModel card = owner.RunState.CreateCard(ModelDb.Card<T>(), owner);
        var added = await CardPileCmd.Add(card, PileType.Deck);
        CardCmd.PreviewCardPileAdd(added);
    }

    // 直接获得一件遗物（对应塔1 spawnRelicAndObtain）。
    protected async Task ObtainRelic<T>() where T : RelicModel
    {
        RelicModel relic = ModelDb.Relic<T>().ToMutable();
        await RelicCmd.Obtain(relic, Owner!);
    }
}
