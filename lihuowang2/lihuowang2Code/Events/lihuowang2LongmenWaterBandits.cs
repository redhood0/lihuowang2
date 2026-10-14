using System.Collections.Generic;
using System.Threading.Tasks;
using lihuowang2.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Events;

// 塔1：龙门寨水匪（Act1）。
// 流程：拦路 → ①认怂（把钱全交出去）→ 收尾；
//              ②杀光他们（打一场 1 层的海傍恶徒）→ 战后回到本事件 →
//                 二选一：a) 拿走《大千录残卷》+ 金币；b) 只拿走金币。
//
// ⚠ 开战的事件**必须**是 shared：引擎 EventModel.EnterCombatWithoutExitingEvent 第一句就是
//   `if (!IsShared) throw new InvalidOperationException("Tried to enter combat in non-shared event")`。
//   开战 → 战后用 Resume(AbstractRoom) 回事件页，是官方「DenseVegetation（密林）」的同一套写法
//   （引擎注释原文：This is most common in events where a choice can start a combat）。
//
// 只在 **1 层** 出现：与「第二附属医院」同理，只注册 1 层的两个 Act（Index == 0）：Overgrowth / Underdocks。
//
// 立绘先用**官方事件图**占位（原版 `res://images/events/slippery_bridge.png`，一条被拦住的桥，正合"拦路"）；
// 想换成自己的图画一张放 `images/event/LongmenWaterBandits.png`，把下面这行路径改成
// `$"{Entry.ResPath}/images/event/LongmenWaterBandits.png"` 即可（背景同样走引擎默认事件背景）。
[RegisterActEvent(typeof(Overgrowth))]
[RegisterActEvent(typeof(Underdocks))]
public sealed class lihuowang2LongmenWaterBandits : lihuowang2EventTemplate
{
    // ===== 效果参数（都集中在这里）=====

    // 战后奖励的金币数：**一个参数**，每次进入本事件在 30~60 之间随机掷一次。
    // 文案里不手写数字，用 {Gold} 引用它 —— 引擎渲染事件页时会自动把事件变量注入页面描述、
    // 选项标题与选项描述（NEventRoom.SetDescription / NEventOptionButton._Ready 里的
    // `Event.DynamicVars.AddTo(...)`），所以改这两行，中英文案自动跟着变，不用再回去改 JSON。
    private const int GoldRollMin = 30;
    private const int GoldRollMax = 60;

    // 拦路的那场战斗用哪个遭遇：1 层的「海傍恶徒」（SeapunkNormal，Underdocks 的普通遭遇，一群水匪）。
    // 想换别的 1 层人形遭遇就改这里（同层可选的还有 HauntedShipNormal 鬼船船工、CultistsNormal 邪教徒）。
    private static EncounterModel BanditEncounter => ModelDb.Encounter<SeapunkNormal>();

    // 开战要求：见类注释（引擎硬性约束）。
    public override bool IsShared => true;

    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/event/LongmenWaterBandits.png");

    // 事件变量：只有金币一项（GoldVar 的名字就是 "Gold"，文案里写 {Gold}）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new GoldVar(0)];

    // 掷金币数：引擎在事件开始时调一次（EventModel.BeginEvent → CalculateVars），
    // 用的是事件自己的 RNG（EventModel.Rng，种子 = 本局种子 + 事件哈希 → 联机各端掷出同一个数）。
    // ⚠ NextInt 的上限是开区间，所以 +1 才是「含 60」。
    public override void CalculateVars()
        => DynamicVars.Gold.BaseValue = Rng.NextInt(GoldRollMin, GoldRollMax + 1);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, YieldGold, InitialOptionKey("YIELD")),
            new EventOption(this, FightThem, InitialOptionKey("FIGHT")),
        ];
    }

    // ① 认怂：把身上的钱全都交出去（GoldLossType.Stolen = 被"抢"走，跑图历史里的记账方式）
    private async Task YieldGold()
    {
        await PlayerCmd.LoseGold(Owner!.Gold, Owner, GoldLossType.Stolen);

        SetEventState(PageDescription("YIELD_DONE"),
        [
            new EventOption(this, Leave, ModOptionKey("YIELD_DONE", "LEAVE")),
        ]);
    }

    private Task Leave()
    {
        // 认怂路线**单独**一条收尾文案（YIELD_END）：
        // 这条路上没钱也没捡到竹简，所以不能说"把竹简塞进怀里"——只有拍拍土继续赶路。
        SetEventFinished(PageDescription("YIELD_END"));
        return Task.CompletedTask;
    }

    // ② 杀光他们：开一场战斗，并在战斗结束后回到本事件（见 Resume）
    private Task FightThem()
    {
        EnterCombatWithoutExitingEvent(BanditEncounter, [], shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    // 战斗结束回到事件：摆出战利品页（两个选项，金币数是同一个值，区别只在要不要那卷竹简）
    public override Task Resume(AbstractRoom exitedRoom)
    {
        SetEventState(PageDescription("WON"),
        [
            // ① 竹简 + 金币。hover 提示：把《大千录残卷》的卡面挂在这个选项上 ——
            //    鼠标悬停即可看清它是什么牌，不用等拿到手
            //    （与「第二附属医院」给"安定"挂的是同一套写法）。
            new EventOption(this, TakeScrollAndGold, ModOptionKey("WON", "TAKE"),
                HoverTipFactory.FromCard<lihuowang2Daqianlu>()),

            // ② 只要钱，那卷来路不明的竹简留在原地。
            new EventOption(this, TakeGoldOnly, ModOptionKey("WON", "GOLD")),
        ]);
        return Task.CompletedTask;
    }

    // ①《大千录残卷》进卡组 + 金币（金额取事件变量 Gold，与卡面显示的是同一个数）
    private async Task TakeScrollAndGold()
    {
        await ObtainCardToDeck<lihuowang2Daqianlu>();
        await PlayerCmd.GainGold(DynamicVars.Gold.IntValue, Owner!);
        SetEventFinished(PageDescription("END"));
    }

    // ② 只捡钱，不要竹简
    private async Task TakeGoldOnly()
    {
        await PlayerCmd.GainGold(DynamicVars.Gold.IntValue, Owner!);
        SetEventFinished(PageDescription("GOLD_END"));
    }
}
