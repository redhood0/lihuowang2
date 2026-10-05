using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 火莲（Boss 级）：最大能量 +1；每场战斗开始时向抽牌堆塞 3 张灼烧。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_HuoLian : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 最大能量 +1（持有期间一直生效，等价原版 energyMaster +1）。
    // ⚠ 必须判 player != Owner：引擎给每个玩家算最大能量时（PlayerCombatState.MaxEnergy）
    //   都会问一遍场上所有遗物，官方给能量的遗物（EnergyMaster 等）全都这么筛；
    //   不筛就会连队友的最大能量一起 +1（单人看不出来，只有组队才暴露）。
    //   模组里「一炁·心素」「XiuZhenPower」是同样写法，可互相参照。
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => player == Owner ? amount + 1m : amount;

    public override async Task BeforeCombatStart()
    {
        // 洗入抽牌堆：随机位置
        await CardPileCmd.AddToCombatAndPreview<Burn>(Owner.Creature, PileType.Draw, 3, Owner!,
            CardPilePosition.Random);
        await base.BeforeCombatStart();
    }
}
