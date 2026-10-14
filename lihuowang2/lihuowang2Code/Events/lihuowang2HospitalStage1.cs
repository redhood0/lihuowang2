using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔1：第二附属医院（HospitalStage1，Act1）。
// 流程：起始三句大喊 → 被打镇定剂 → 获得“安定”→ 昏睡收尾。
//
// 只在 **1 层** 出现：本作 1 层有两个可能的阶段模型（ActModel.Index == 0）——
//   · Overgrowth（Act1 的另一套地图）
//   · Underdocks（Index 0、ChestOpenSfx = treasure_act1）
// 所以只注册这两个；2 层 Hive（Index 1）、3 层 Glory（Index 2）不注册，
// 事件就不会出现在下层（原来四个 Act 全注册但整段被注释 → 事件根本不出现）。
//
// 背景：沿用原版事件的默认背景 —— AssetProfile 只覆盖立绘 InitialPortraitPath，
// BackgroundScenePath 留空即走引擎默认事件背景（模组里另外两个事件也是这么做的）。
[RegisterActEvent(typeof(Overgrowth))]
[RegisterActEvent(typeof(Underdocks))]
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
            // hover 提示：把「安定」的卡面挂在这个选项上 —— 鼠标悬停即可看清它的费用与效果，
            // 不用等拿到手。写法用 EventOption 的 params IHoverTip[] 重载（官方事件也是这么挂的）。
            new EventOption(this, ObtainAnding, ModOptionKey("SEDATE", "GET_ANDING"),
                HoverTipFactory.FromCard<lihuowang2AnDing>()),
        ]);
        return Task.CompletedTask;
    }

    private async Task ObtainAnding()
    {
        await ObtainCardToDeck<lihuowang2AnDing>();
        SetEventFinished(PageDescription("OUT"));
    }
}
