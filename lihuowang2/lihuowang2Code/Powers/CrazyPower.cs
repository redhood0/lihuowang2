using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

[RegisterPower]
public class CrazyPower : ModPowerTemplate
{
    // 剩余回合数的变量名，本地化里用 {ExtraTurns} 显示
    private const string ExtraTurnsVarName = "ExtraTurns";

    // 类型，Buff或Debuff
    public override PowerType Type => PowerType.Buff;
    // 叠加类型，Counter表示可叠加，Single表示不可叠加
    public override PowerStackType StackType => PowerStackType.Single;

    // 自定义图标路径。1:1即可。原版游戏大图256x256，小图64x64。
    public override PowerAssetProfile AssetProfile => new(
        IconPath:$"{Entry.ResPath}/images/powers/fengdian32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/fengdian84.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar(ExtraTurnsVarName, 1m)
    ];

    // 再次施加时刷新持续时间
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
            DynamicVars[ExtraTurnsVarName].BaseValue = 1m;

        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    // 受到伤害减少25%，造成伤害增加50%
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 造成伤害增加50%。
        // 用 IsPoweredAttack：不受力量影响的攻击（Unpowered）不该吃到攻击端加成，与虚弱/力量一致。
        if (dealer == Owner && props.IsPoweredAttack())
            return 1.5m;

        // 受到伤害减少25%。
        // 用 IsCardOrMonsterMove：只要来自攻击就减伤，不受 Unpowered 影响。
        // 减伤属于防御端效果，若用 IsPoweredAttack 会被敌人的 Unpowered 攻击绕过。
        // if (target == Owner && props.IsCardOrMonsterMove())
        //     return 0.75m;

        return 1m;
    }
    
    // 受到伤害减少25%（只影响真实扣血，不影响怪物意图显示）
    public override decimal ModifyHpLostBeforeOsty(
        Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner) return amount;
        if (!props.HasFlag(ValueProp.Move)) return amount;   // 只对攻击伤害
        return amount * 0.75m;
    }
    
    // // lihuowang2Relic_Xinsu.cs 里加：李火旺受到的最终伤害 -1（下限 0）
    // public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount,
    //     ValueProp props, Creature? dealer, CardModel? cardSource)
    // {
    //     // 只对自己生效
    //     if (target != Owner?.Creature) return amount;
    //     return Math.Max(amount - 1m, 0m);
    // }

    // 自己的回合结束时倒计时，归零后自动移除
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        if ((int)DynamicVars[ExtraTurnsVarName].BaseValue > 0)
        {
            DynamicVars[ExtraTurnsVarName].BaseValue -= 1m;
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
    }
}
