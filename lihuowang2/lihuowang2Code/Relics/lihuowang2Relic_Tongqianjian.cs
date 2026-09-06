using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 铜钱剑：胜利时按手中铜钱数 × 随机(10~15) 获得金币。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Tongqianjian : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        MoneyPower? copper = Owner.Creature.GetPower<MoneyPower>();
        if (copper != null && copper.Amount > 0m && Owner.Creature.Player != null)
        {
            decimal gold = copper.Amount * Random.Shared.Next(10, 16);
            await PlayerCmd.GainGold(gold, Owner.Creature.Player);
        }

        await base.AfterCombatVictory(room);
    }
}
