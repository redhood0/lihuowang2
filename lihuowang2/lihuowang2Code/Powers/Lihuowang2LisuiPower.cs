using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 李岁·狗形态：每个自己回合开始对所有敌人造成 Damage 点真实伤害；
// 触手牌造成的伤害与获得的格挡 + Boost。
[RegisterPower]
public class Lihuowang2LisuiPower : ModPowerTemplate
{
    // 每回合对全体敌人造成的伤害
    public decimal Damage { get; set; } = 3m;
    // 触手牌的伤害/格挡加成
    public decimal Boost { get; set; } = 2m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/lisui32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/lisui84.png");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        Flash();

        IReadOnlyList<Creature> enemies = Owner.CombatState!.Enemies;
        foreach (Creature enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            await CreatureCmd.Damage(choiceContext, enemy, Damage,
                ValueProp.Unblockable, Owner, null, null);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 触手攻击伤害加成
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || !IsTentacle(cardSource))
            return 0m;
        return Boost;
    }

    // 触手格挡加成
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || !IsTentacle(cardSource))
            return 0m;
        return Boost;
    }

    private static bool IsTentacle(CardModel? card) => card switch
    {
        lihuowang2TentacleSlash or lihuowang2TentacleBind or
        lihuowang2TentacleMend or lihuowang2TentacleEatGhost => true,
        _ => false
    };
}
