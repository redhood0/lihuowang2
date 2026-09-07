using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 铜镜：当你受到易伤/虚弱时，将所有敌人也同样施加等量。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Tongjing : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 只镜像施加到自己身上的易伤/虚弱
        if (power.Owner != Owner.Creature || amount <= 0m ||
            power is not VulnerablePower and not WeakPower)
        {
            await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
            return;
        }

        Flash();

        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        foreach (Creature enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            if (power is VulnerablePower)
                await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, amount, applier, cardSource);
            else
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, amount, applier, cardSource);
        }

        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }
}
