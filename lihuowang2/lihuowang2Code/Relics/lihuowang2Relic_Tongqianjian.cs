using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 铜钱剑：胜利时按手中铜钱数 × 随机(10~15) 获得金币。
//
// ⚠ 为什么要拆成两个钩子（这是本遗物曾经"有铜钱却不给钱"的原因）：
//   引擎处理战斗胜利的顺序是（CombatManager.EndCombatInternal）：
//     Hook.AfterCombatEnd(...)                     ← 此时玩家身上的能力还在
//     player.AfterCombatEnd()                      ← 内部 RemoveAllPowersInternalExcept()：铜钱被清掉
//     Hook.AfterCombatVictory(...)                 ← 我们原来在这里读铜钱 → 永远是 null
//   所以：AfterCombatEnd 里先把铜钱数**记下来**，AfterCombatVictory 里再按记下的数发钱。
//   （胜利与否仍由 AfterCombatVictory 把关 —— 那条路径只在战胜时走到，失败走 ProcessPendingLoss，
//     不会触发 AfterCombatVictory。）
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Tongqianjian : ModRelicTemplate
{
    // 战斗结束那一刻手上的铜钱数（在能力被清掉之前记下）。
    // 用 [SavedProperty]：这两个钩子之间可能插进存档/多人快照，普通字段会丢。
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CopperAtCombatEnd { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 战斗结束（能力还在）→ 记下铜钱数
    public override Task AfterCombatEnd(CombatRoom room)
    {
        CopperAtCombatEnd = (int)(Owner.Creature.GetPower<MoneyPower>()?.Amount ?? 0m);
        return base.AfterCombatEnd(room);
    }

    // 胜利结算 → 按记下的铜钱数发金币
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        // 先取走并清零，保证任何情况下都不会重复结算
        int copper = CopperAtCombatEnd;
        CopperAtCombatEnd = 0;

        Player? player = Owner.Creature.Player;
        if (copper > 0 && player != null)
        {
            Flash();
            // 多人同步：随机金额走引擎的确定性随机流（Niche 隔离，不会消耗掉奖励流）
            decimal gold = copper * player.RunState.Rng.Niche.NextInt(10, 16);
            await PlayerCmd.GainGold(gold, player);
        }

        await base.AfterCombatVictory(room);
    }
}
