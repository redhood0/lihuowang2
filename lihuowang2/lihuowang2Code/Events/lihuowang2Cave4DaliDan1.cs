using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using lihuowang2.Characters;
using lihuowang2.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔1：深山洞府（Cave4DaliDan1，Act1）。
// 流程：跟随玄阳→抢下玉佩；或反方向告密→赏大力丹；最后以“两眼一黑”收尾。
// [RegisterActEvent(typeof(Overgrowth))]
// [RegisterActEvent(typeof(Underdocks))]
// [RegisterActEvent(typeof(Glory))]
// [RegisterActEvent(typeof(Hive))]
public sealed class lihuowang2Cave4DaliDan1 : lihuowang2EventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/Cave4DaliDan1.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, GoWithThem, InitialOptionKey("GO_WITH")),
            new EventOption(this, ReportTeacher, InitialOptionKey("REPORT")),
        ];
    }

    // 玄阳一伙：被捂住嘴拽下玉佩。
    private Task GoWithThem()
    {
        SetEventState(PageDescription("YUPEI"),
        [
            new EventOption(this, TakeYupei, ModOptionKey("YUPEI", "GET_YUPEI")),
        ]);
        return Task.CompletedTask;
    }

    private async Task TakeYupei()
    {
        await ObtainRelic<lihuowang2Relic_XuanyangYupei>();
        SetEventFinished(PageDescription("END"));
    }

    // 打小报告：师父赏丹药。
    private Task ReportTeacher()
    {
        SetEventState(PageDescription("DALI"),
        [
            new EventOption(this, TakeDaliDan, ModOptionKey("DALI", "GET_DALI_DAN")),
        ]);
        return Task.CompletedTask;
    }

    private async Task TakeDaliDan()
    {
        await ObtainCardToDeck<lihuowang2DaLiDan>();
        SetEventFinished(PageDescription("END"));
    }
}
