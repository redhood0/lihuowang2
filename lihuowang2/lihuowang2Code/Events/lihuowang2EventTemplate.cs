using System.Threading.Tasks;
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
