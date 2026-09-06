using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔1：第二附属医院（HospitalStage1，Act1）。
// 流程：起始三句大喊 → 被打镇定剂 → 获得“安定”→ 昏睡收尾。
// [RegisterActEvent(typeof(Overgrowth))]
// [RegisterActEvent(typeof(Underdocks))]
// [RegisterActEvent(typeof(Glory))]
// [RegisterActEvent(typeof(Hive))]
public sealed class lihuowang2HospitalStage1 : lihuowang2EventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/HospitalStage1.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, OnYellNosick, InitialOptionKey("YELL_NOSICK")),
            new EventOption(this, OnYellSick, InitialOptionKey("YELL_SICK")),
            new EventOption(this, OnYellMother, InitialOptionKey("YELL_MOTHER")),
        ];
    }

    // 不管喊什么都会被镇静剂放倒。
    private Task OnYellNosick() => ShowSedatePage();
    private Task OnYellSick() => ShowSedatePage();
    private Task OnYellMother() => ShowSedatePage();

    private Task ShowSedatePage()
    {
        SetEventState(PageDescription("SEDATE"),
        [
            new EventOption(this, ObtainAnding, ModOptionKey("SEDATE", "GET_ANDING")),
        ]);
        return Task.CompletedTask;
    }

    private async Task ObtainAnding()
    {
        await ObtainCardToDeck<lihuowang2AnDing>();
        SetEventFinished(PageDescription("OUT"));
    }
}
