using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
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
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        // 轮廓图标（原版 85x85）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        // 大图标（原版 256x256）。
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 每回合开始时，抽一张牌。
    // 这里使用 DynamicVars.Cards.IntValue，保证效果和本地化显示保持一致。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
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
        _stepCount++;
        InvokeDisplayAmountChanged();
    }

}