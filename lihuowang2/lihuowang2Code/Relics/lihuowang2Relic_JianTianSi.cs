using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 监天司令牌：击杀邪祟后按档次领赏金 —— 普通怪 10 金币 / 精英 40 / BOSS 50。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_JianTianSi : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 三档赏金。名字分开取，方便卡面/遗物说明里分别引用。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("NormalGold", 10m),
        new DynamicVar("EliteGold", 40m),
        new DynamicVar("BossGold", 50m)
    ];

    // 战斗「胜利」时结算赏金（失败/逃走走的是另一条路，不会触发这个钩子）。
    // 房间档次取 room.RoomType（= Encounter.RoomType），事件里打起来的架会算成普通怪。
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        decimal bounty = room.RoomType switch
        {
            RoomType.Monster => DynamicVars["NormalGold"].BaseValue,
            RoomType.Elite => DynamicVars["EliteGold"].BaseValue,
            RoomType.Boss => DynamicVars["BossGold"].BaseValue,
            _ => 0m
        };

        // 非战斗房间（宝箱/商店等）理论上不会走到这，兜底跳过
        if (bounty <= 0m)
            return;

        Flash();
        await PlayerCmd.GainGold(bounty, Owner);

        await base.AfterCombatVictory(room);
    }
}
