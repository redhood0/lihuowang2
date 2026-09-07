using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔1：祆景教洞府（AoJingJiao2，Act2）。
// 流程：听手叁讲登阶真义（文本收尾）；或救姜英子得到《火袄箴经》（火袄真经卡）。
// [RegisterActEvent(typeof(Overgrowth))]
// [RegisterActEvent(typeof(Underdocks))]
// [RegisterActEvent(typeof(Glory))]
// [RegisterActEvent(typeof(Hive))]
public sealed class lihuowang2AoJingJiao2 : lihuowang2EventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/AoJingJiao2.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, AskDengJieDetails, InitialOptionKey("ASK_DETAILS")),
            new EventOption(this, RescueJiangYingzi, InitialOptionKey("RESCUE")),
        ];
    }

    // 了解登阶真义：听完便晕，无奖励，直接收尾。
    private Task AskDengJieDetails()
    {
        SetEventFinished(PageDescription("TRUE_MEANING"));
        return Task.CompletedTask;
    }

    // 救下姜英子：学会火袄真经（把卡加入卡组）。
    private Task RescueJiangYingzi()
    {
        SetEventState(PageDescription("RESCUE_DONE"),
        [
            new EventOption(this, TakeHuowoZhenjin, ModOptionKey("RESCUE_DONE", "GET_BOOK")),
        ]);
        return Task.CompletedTask;
    }

    private async Task TakeHuowoZhenjin()
    {
        await ObtainCardToDeck<lihuowang2HuoWoZhenJin>();
        SetEventFinished(PageDescription("BOOK_RECEIVED"));
    }
}
