using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using lihuowang2.Characters;
using lihuowang2.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// RegisterRelic 会把遗物注册进指定遗物池。
// RegisterCharacterStarterRelic 会把它作为 lihuowang2Character 的初始遗物。
[RegisterRelic(typeof(lihuowang2RelicPool))]
[RegisterCharacterStarterRelic(typeof(lihuowang2Character))]
public sealed class lihuowang2Relic_Xinsu : ModRelicTemplate
{
    // 本场战斗内累计抽到的疑虑数量，满3张后清零。跨回合累计，不跨战斗。
    private int _doubtDrawnCount;

    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Common;

    // 计数器：只在积累了疑虑后才显示，默认（0）不显示
    public override bool ShowCounter => _doubtDrawnCount > 0;

    // 计数器显示的数字，与累计的疑虑数量一致
    public override int DisplayAmount => _doubtDrawnCount;

    // protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromCardWithCardHoverTips<Soul>();

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Doubt>(),
        // .. HoverTipFactory.FromCardWithCardHoverTips<Doubt>()
    ];
    // HoverTipFactory.FromPower<BlurPower>(),
    // HoverTipFactory.FromKeyword(MyKeywords.Unique)
    // 通过HoverTipFactory添加各种提示文本


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

    // // 每回合开始时，抽一张牌。
    // // 这里使用 DynamicVars.Cards.IntValue，保证效果和本地化显示保持一致。
    // public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    // {
    //     await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    // }
    
    //这里写方法，回合开始时获得1长张疑虑

    public override async   Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        await CardPileCmd.AddToCombatAndPreview<Doubt>(player.Creature, PileType.Hand, 1, player);
        // 生成的疑虑是直接放进手牌的，不会触发抽牌钩子，因此在这里直接计入
        await CountDoubtAndTryTriggerCrazy(choiceContext);
        await base.AfterPlayerTurnStartEarly(choiceContext, player);
    }

    // public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    // {
    //     await CardPileCmd.AddToCombatAndPreview<Doubt>(player.Creature, PileType.Hand, 1, player);
    //     // 生成的疑虑是直接放进手牌的，不会触发抽牌钩子，因此在这里直接计入
    //     await CountDoubtAndTryTriggerCrazy(choiceContext);
    // }

    // 每场战斗开始时清零，使统计只在单场战斗内累计
    public override async Task BeforeCombatStart()
    {
        _doubtDrawnCount = 0;
        UpdateCounterDisplay();
        await base.BeforeCombatStart();
    }

    //这里写方法，每抽到3张疑虑，获得Crazypower的buff
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        // 只统计真正抽到手的疑虑
        if (card is not Doubt) return;

        await CountDoubtAndTryTriggerCrazy(choiceContext);
    }

    // 累计1张疑虑，满3张时施加疯癫并清零
    private async Task CountDoubtAndTryTriggerCrazy(PlayerChoiceContext choiceContext)
    {
        _doubtDrawnCount++;
        UpdateCounterDisplay();

        if (_doubtDrawnCount < 3) return;

        _doubtDrawnCount = 0;
        UpdateCounterDisplay();
        await PowerCmd.Apply<CrazyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
    }

    // 通知UI刷新计数器显示
    private void UpdateCounterDisplay()
    {
        InvokeDisplayAmountChanged();
    }

    // 战斗结束后清空计数器（不跨战斗保留）
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        _doubtDrawnCount = 0;
        UpdateCounterDisplay();
        await base.AfterCombatEnd(room);
    }

}