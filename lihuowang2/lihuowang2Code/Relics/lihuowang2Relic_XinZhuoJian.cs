using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Cards;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 心灼剑：每场战斗开始时，将一张「破碎虚空斩」加入手牌。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_XinZhuoJian : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task BeforeCombatStart()
    {
        await CardPileCmd.AddToCombatAndPreview<lihuowang2BreakSpace>(
            Owner.Creature, PileType.Hand, 1, Owner!);
        await base.BeforeCombatStart();
    }
}
