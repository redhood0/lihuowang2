using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Encounters;

// 「大船」事件专用遭遇：**2 只精英怪** —— 随机 1 只来自第 3 层（Glory），
// 随机 1 只来自第 2 层（Hive），一起上。
//
// 几个设计点：
//   · [RegisterGlobalEncounter] 把模型登记进 ModelDb（事件里要 ModelDb.Encounter<T>() 拿它）。
//     注意它同时也会把遭遇追加进**所有章节**的遭遇池，必须靠下面的 IsValidForAct 返回 false 摘出去。
//   · ShouldGiveRewards = false：这是事件战，奖励由事件自己发（2 件稀有遗物 + 88 金币），
//     否则打完还会再弹一次普通的战斗奖励（金币/卡牌），变成两份额外奖励。
//   · 只用**单只怪**的精英遭遇：
//       - 第 3 层的 KnightsElite 是三个骑士（FlailKnight + SpectralKnight + MagiKnight），
//       - 第 2 层的 DecimillipedeElite 是三节虫（还带 StarterMoveIdx 轮转初始化），
//     只抠一只出来会缺胳膊少腿，所以两个池子里都不放它们。
[RegisterGlobalEncounter]
public sealed class lihuowang2BigShipEncounter : ModEncounterTemplate
{
    // 事件战按普通战斗处理（不进精英/Boss 专属流程，不触发它们的 UI/BGM 和结算）。
    public override RoomType RoomType => RoomType.Monster;

    // 见类注释：奖励由事件发放，关掉常规战斗奖励。
    public override bool ShouldGiveRewards => false;

    // ⚠ 这几个字是必须的：RitsuLib 会把 [RegisterGlobalEncounter] 的遭遇**追加进每个章节的遭遇池**
    //   （ActModel.AllEncounters 的 postfix），再由 IModEncounterActValidity 过滤。默认实现返回 true，
    //   也就是"允许出现在所有章节"——那样平时走地图就会随机撞上这场双精英，直接把人打死。
    //   返回 false 只是把它从普通/精英/首领池里摘掉，ModelDb 里仍然存在，事件照样能 ModelDb.Encounter<T>() 拉起来。
    public override bool IsValidForAct(ActModel act) => false;

    // 池子用表达式而不是 static 字段：ModelDb 的初始化时机比静态构造晚，
    // 写成静态字段有概率在 ModelDb 还没准备好时就去取模型。
    //
    // 第 3 层（Glory）的单只怪精英：MechaKnight / SoulNexus。
    private static EncounterModel[] Act3EliteEncounters =>
    [
        ModelDb.Encounter<MechaKnightElite>(),
        ModelDb.Encounter<SoulNexusElite>(),
    ];

    // 第 2 层（Hive）的单只怪精英：Entomancer / InfestedPrism。
    private static EncounterModel[] Act2EliteEncounters =>
    [
        ModelDb.Encounter<EntomancerElite>(),
        ModelDb.Encounter<InfestedPrismsElite>(),
    ];

    // 预加载 / 图鉴用：这场仗里可能出现的所有怪。
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        Act3EliteEncounters.SelectMany(e => e.AllPossibleMonsters)
            .Concat(Act2EliteEncounters.SelectMany(e => e.AllPossibleMonsters));

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        // Rng 由 EncounterModel.GenerateMonstersWithSlots 用「存档种子 + 层数 + 遭遇 id」算出来，
        // 是确定性的，联机两端会抽到同一组怪。
        MonsterModel fromAct3 = Rng.NextItem(Act3EliteEncounters.SelectMany(e => e.AllPossibleMonsters))!;
        MonsterModel fromAct2 = Rng.NextItem(Act2EliteEncounters.SelectMany(e => e.AllPossibleMonsters))!;

        // slot 传 null：这几只怪在原遭遇里也都是 null slot，交给引擎自动摆位。
        return
        [
            (fromAct3.ToMutable(), null),
            (fromAct2.ToMutable(), null),
        ];
    }
}
