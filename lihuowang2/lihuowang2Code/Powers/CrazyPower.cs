using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
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

    // 描述里用了 {ExtraTurns} 这个自定义变量。
    // 引擎默认的「dumb」悬停（卡牌 hover、牌库等）只注入 Amount 等通用变量，不注入能力自己的 DynamicVars，
    // 那样 {ExtraTurns} 会解析失败、整条文本退化成原文，所以这里补上。
    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            DynamicVars.AddTo(description);
            return description;
        }
    }

    // 再次施加时刷新持续时间
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
            DynamicVars[ExtraTurnsVarName].BaseValue = 1m;

        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
    }

    // 受到伤害减少25%，造成伤害增加50%。
    //
    // 两条都必须写在这「伤害计算」这一档（ModifyDamageMultiplicative）：
    // 引擎 CreatureCmd.Damage 的实际顺序是
    //   ① ModifyDamage（含本钩子）→ ② 用①的结果扣格挡 → ③ ModifyHpLost → 扣生命值
    // 写在①才是「伤害整体先打七五折 → 格挡按折扣后的数字吸收 → 剩下的才扣血」。
    // （不要挪回 ModifyHpLostBeforeOsty：引擎文档写明那个钩子 "runs ... after block is applied"，
    //   写在那边就退化成「先按原价扣格挡、余额再减25%」，正是要避免的行为。）
    //
    // 两个分支互斥（都用「对方必须不是自己」判定），避免互相吃掉。
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        decimal multiplier = 1m;

        // 攻击端：我打别人 → 造成伤害 +50%。
        // 用 IsPoweredAttack：不受力量影响的攻击（Unpowered）不该吃到攻击端加成，与虚弱/力量一致。
        if (dealer == Owner && target != Owner && props.IsPoweredAttack())
        {
            multiplier = 1.5m;
        }
        // 防御端：别人打我 → 受到伤害 -25%。
        //
        // ⚠⚠ 判定必须**完全确定性**：同一笔伤害在所有客户端必须算出同一个值，否则联机直接判「状态分歧」。
        //   所以这里只能认 target == Owner（power 挂在受击者身上，所有客户端都是同一个 creature 实例）。
        //   绝对不要用「本机判定」：
        //     - LocalContext.IsMe(target.Player)：只有受击者自己的机器返回 true → 那台机器算 0.75，
        //       队友的机器对同一笔伤害算 1.0 → HP/格挡两端不同 → 校验和分歧（这就是之前"双人不同步"的原因）；
        //     - target.IsPlayer：队友的 creature 同样满足 IsPlayer，而且怪物意图是引擎按「本机玩家」算的，
        //       用它会把我的疯癫套到队友的意图数字上。
        //   单人下 target == Owner 与上述口径完全等价，所以行为没有任何变化。
        // 不限制 props（卡面写的是「受到伤害减少25%」，中毒/持续伤害等非攻击伤害也该吃）。
        // dealer == target 视为自伤（自伤牌、点燃反噬），不减。
        // 与其它乘算修正相乘叠加（例如易伤 1.5：0.75 × 1.5 = 1.125）。
        else if (target == Owner && dealer != target)
        {
            multiplier = 0.75m;
        }

        return multiplier;
    }

    // 已知行为（2026-10-01 确认，2026-10-02 修多人不同步）：减伤会反映到「本机玩家看到的怪物意图」数字上。
    //
    // 原因：意图的伤害数字走的是同一套伤害钩子 ——
    //   SingleAttackIntent / MultiAttackIntent.GetSingleDamage() 会调用
    //   Hook.ModifyDamage(..., me.Creature, owner, DamageCalc(), ValueProp.Move, null, null, All, None, out _)
    //   即「target = 本机玩家、dealer = 怪物、cardSource = null」，与上面的减伤分支吻合，
    //   于是本机看到的意图数字就是实际会打在格挡/生命上的数字（10 点攻击显示 7）。比原值更准，保留。
    // ⚠ 该路径的 target 是「各客户端自己的玩家」，所以意图数字天然是「各看各的」（引擎设计如此，
    //   官方减伤遗物也一样）；但这只影响显示，**不能因此把判定改成 IsMe/LocalContext** ——
    //   那会让真正结算那一笔伤害也变成"按机器"计算，两端数值不同直接触发状态分歧。
    //
    // 以后若要「意图显示原值、实际减伤」，可选方案（都不完美，需要时再评估）：
    //   A. 把减伤挪回 ModifyHpLostBeforeOsty（格挡之后）：意图不受影响，但会退回
    //      「先按原价扣格挡、余额再减25%」的旧语义，与本文件上方的目标相反；
    //   B. 用 Harmony 补丁改写意图那条路径（GetSingleDamage / GetTotalDamage），只让显示层忽略本 power，
    //      代价是要跟着引擎版本维护补丁；
    //   C. 若引擎后续给 Hook.ModifyDamage 增加能区分「意图预览」的参数（现在两条路径传的
    //      CardPreviewMode 都是 None，无法区分），直接用那个参数即可。

    // // lihuowang2Relic_Xinsu.cs 里加：李火旺受到的最终伤害 -1（下限 0）
    // public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount,
    //     ValueProp props, Creature? dealer, CardModel? cardSource)
    // {
    //     // 只对自己生效
    //     if (target != Owner?.Creature) return amount;
    //     return Math.Max(amount - 1m, 0m);
    // }

    // 敌人回合结束时移除（即下一个玩家回合开始前）。
    // 疯癫获得后会覆盖「获得当回合 + 紧接的敌人回合」，到敌方回合结束就消失。
    // 不用 AfterPlayerTurnStart，因为疯癫本身是心素在 AfterPlayerTurnStart 里施加的，
    // 挂同一个钩子会在施加的瞬间被自己移除（一获得就消失）。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 只在敌人回合结束时移除
        if (side != CombatSide.Enemy)
            return;

        Flash();
        await PowerCmd.Remove(this);
    }

    
    
}
