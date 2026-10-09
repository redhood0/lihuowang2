using lihuowang2.Characters;
using lihuowang2.Tags;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Dengjie : ModRelicTemplate
{
    // 计数器上限（登阶满 10 层）
    private const int MaxSteps = 10;
    // 计数器当前值，默认 1
    private int _stepCount = 1;

    // 登阶计数的存档出口：[SavedProperty] 的属性会被引擎写进本局存档并在读档时回填，
    // 没有它的话「保存退出 → 继续游戏」会把计数器退回到字段初始值 1（玩家看到的现象）。
    // 官方同类遗物（蜥蜴尾巴 WasUsed、古老牙齿 StarterCard）都是这个写法。
    [SavedProperty]
    public int StepCount
    {
        get => _stepCount;
        private set
        {
            _stepCount = value;
            InvokeDisplayAmountChanged();
        }
    }

    // 始终显示计数器数字
    public override bool ShowCounter => true;

    // 计数器显示的数字（不会超过上限）
    public override int DisplayAmount => Math.Clamp(_stepCount, 0, MaxSteps);

    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Event;
    
    

    // 遗物的数值。这里会替换本地化中的 {Cards}。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    // 图片资源统一放在 AssetProfile 里配置。
    // 三个路径可以先指向同一张图。后续有高清图或轮廓图时再拆开。
    public override RelicAssetProfile AssetProfile => new(
        // 小图标（原版 85x85）。
        IconPath: $"{Entry.ResPath}/images/relics/bahui.png",
        // 轮廓图标（原版 85x85）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/bahui.png",
        // 大图标（原版 256x256）。
        BigIconPath: $"{Entry.ResPath}/images/relics/bahui.png");

    // 每回合开始时，抽一张牌。
    // 这里使用 DynamicVars.Cards.IntValue，保证效果和本地化显示保持一致。
    // 注意：本遗物的文案里没有「抽牌」（只有计数器 + 战斗开始给再生），
    // 所以这段效果保持注释状态；方法入口留着，以后想加直接放开即可。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人注意：Hook.PlayerTurnStart 会为「每个玩家的回合开始」通知全场所有遗物，
        // 若以后启用，必须判断轮到的是不是遗物持有者，否则会替队友白抽牌。
        // if (player == Owner)
        //     await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);

        await base.AfterPlayerTurnStart(choiceContext, player);
    }
    
    // 战斗开始时
    public override async Task BeforeCombatStart()
    {
        
       
        // 战斗开始：给自己获得「计数器 × 8」层再生效果（默认 1×8 = 8 层，上限 10×8 = 80 层）
        await PowerCmd.Apply<RegenPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            countNum(), Owner.Creature, null);

        await base.BeforeCombatStart();
    }

    public int countNum()
    {
        switch (_stepCount)
        {
            case 1:
                return 8;
            case 2:
                return 8+8/2;
            case 3:
                return 8+8/2+8/4;
            case 4:
                return 8+8/2+8/4+8/8;
            case 5:
                return 8+8/2+8/4+8/8+1;
            case 6:
                return 8+8/2+8/4+8/8+2;
            case 7:
                return 8+8/2+8/4+8/8+3;
            case 8:
                return 8+8/2+8/4+8/8+4;
            case 9:
                return 8+8/2+8/4+8/8+5;
            case 10:
                return 8+8/2+8/4+8/8+6;
        }
        
        return _stepCount;
    }

    // 登阶 +1（上限 10），供登阶卡触发
    public void StepUp()
    {
        if (_stepCount >= MaxSteps)
            return;
        StepCount = _stepCount + 1;   // 走属性，顺带刷新计数器显示
    }

    // ===== 潜规则：每层登阶 → 大千录卡牌伤害 +10% =====

    // 每层加成（10%）。层数就是计数器数值（1~10）→ 满层共 +100%。
    private const decimal DamageBonusPerStep = 0.1m;

    // 只放大"我打出的、带「大千录」tag 的牌"对**敌人**造成的伤害；其余一律不变：
    //   · 别的牌（无该 tag）、队友打的牌 → 不加成；
    //   · 大千录牌对自己的伤害（很多大千录是自伤换效果）→ 不加成；
    //   · 非卡牌来源的伤害（异常/能力/遗物）→ 根本没有 cardSource，不加成。
    // 写法与官方遗物 VitruvianMinion（"带 Minion tag 的牌 ×2 伤害"）一致：按卡牌 tag 判定，不自己维护卡表。
    //
    // 卡面数字是**自动**跟着变的：引擎算卡面伤害（DynamicVar.UpdateCardPreview / CalculatedDamageVar）
    // 走的是同一个 Hook.ModifyDamage → ModifyDamageMultiplicative，所以不需要去改任何卡牌的 DynamicVars。
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // ① 必须是我打出的、且属于大千录系列的牌
        if (cardSource == null || cardSource.Owner != Owner)
            return 1m;
        if (!cardSource.Tags.Contains(DaqianTags.DaqianLu))
            return 1m;

        // ② 只加"打向敌人"的那一份：明确是己方目标（自己/随从）就跳过。
        //    ⚠ target 可能是 null —— 那是卡面预览的路径（牌库里看牌、战斗里未选目标），
        //    这种情况不能当成己方，否则卡面数字就不会动态显示了。
        if (target != null && target.Side != CombatSide.Enemy)
            return 1m;

        return 1m + DamageBonusPerStep * StepCount;
    }
}