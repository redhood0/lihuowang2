using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔2（Act2 = Hive）：正德寺。
// 流程：
//   初始 → ①进入（撞见和尚练怪功，被叫住）→ 开战（2 层普通怪「胧光怪」）→ 战后回事件 → 脱身离开；
//        ②离开（去城里）→ 遇到流浪狗，喂馒头 → 收养，获得「馒头先吃」。
//
// ⚠ 开战的事件必须 IsShared = true（引擎硬性约束，见 LongmenWaterBandits 注释）。
// 只在 **2 层（Act2 = Hive）** 出现：只注册 Hive。
[RegisterActEvent(typeof(Hive))]
public sealed class lihuowang2ZhengdeTemple : lihuowang2EventTemplate
{
    // 战斗用的 2 层普通遭遇：「胧光怪」TheObscuraNormal。
    // 选它是因为它的机制就是**幻象**（遭遇里带 illusionSlot，战败文案原文"和其幻象将你击杀了"），
    // 与本页面"分不清幻觉还是现实"正好呼应；且它确实在 Hive（Act2，Index == 1）的遭遇池里。
    // 想换别的 2 层普通怪就改这里（同层可选：BowlbugsNormal / ChompersNormal / OvicopterNormal / MytesNormal）。
    private static EncounterModel MonkEncounter => ModelDb.Encounter<TheObscuraNormal>();

    // 开战要求：见类注释。
    public override bool IsShared => true;

    // 立绘先用**官方事件图**占位（原版 `res://images/events/zen_weaver.png`，禅/僧人气质，正合"正德寺"）；
    // 之前指向模组自己的 ZhengdeTemple.png（不存在）→ 立绘加载失败会令事件节点初始化崩溃，
    // 表现就是打开全黑、选项也不显示。要换成自己的图，画一张放 `images/event/ZhengdeTemple.png`，
    // 把下面这行改回 `$"{Entry.ResPath}/images/event/ZhengdeTemple.png"` 即可（背景走引擎默认事件背景）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/ZhengdeTemple.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, EnterTemple, InitialOptionKey("ENTER")),
            new EventOption(this, LeaveTemple, InitialOptionKey("LEAVE")),
        ];
    }

    // ①进入：被和尚叫住，开战
    private Task EnterTemple()
    {
        SetEventState(PageDescription("CAUGHT"),
        [
            new EventOption(this, FightMonks, ModOptionKey("CAUGHT", "FIGHT")),
        ]);
        return Task.CompletedTask;
    }

    // 撞见和尚练功后被叫住 → 开战，战后回事件（见 Resume）
    private Task FightMonks()
    {
        EnterCombatWithoutExitingEvent(MonkEncounter, [], shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    // 战斗结束回到事件：脱身页
    public override Task Resume(AbstractRoom exitedRoom)
    {
        SetEventState(PageDescription("ESCAPED"),
        [
            new EventOption(this, Escape, ModOptionKey("ESCAPED", "LEAVE")),
        ]);
        return Task.CompletedTask;
    }

    private Task Escape()
    {
        SetEventFinished(PageDescription("ESCAPED_END"));
        return Task.CompletedTask;
    }

    // ②离开：去城里，遇到流浪狗
    private Task LeaveTemple()
    {
        SetEventState(PageDescription("DOG"),
        [
            // hover 提示：把「馒头先吃」的卡面挂在这个选项上，悬停即可看清。
            new EventOption(this, AdoptDog, ModOptionKey("DOG", "ADOPT"),
                HoverTipFactory.FromCard<lihuowang2ManTouEat>()),
        ]);
        return Task.CompletedTask;
    }

    // 收养流浪狗：获得「馒头先吃」
    private async Task AdoptDog()
    {
        await ObtainCardToDeck<lihuowang2ManTouEat>();
        SetEventFinished(PageDescription("DOG_END"));
    }
}
