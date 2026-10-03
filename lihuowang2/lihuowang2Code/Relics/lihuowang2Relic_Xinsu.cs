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
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// RegisterRelic 会把遗物注册进指定遗物池。
// RegisterCharacterStarterRelic 会把它作为 lihuowang2Character 的初始遗物。
[RegisterRelic(typeof(lihuowang2RelicPool))]
[RegisterCharacterStarterRelic(typeof(lihuowang2Character))]
// 官方遗物「欧洛巴斯之触」（TouchOfOrobas）的效果是"把初始遗物精炼成先祖遗物"，
// 与「古老牙齿」把初始卡超越成先祖卡一一对应。
// 这里把初始遗物 心素 注册成先祖遗物 一炁·心素：拿到那件祝福时，心素会被替换成一炁·心素。
// ⚠ 不注册的话，引擎对"它不认识的初始遗物"会走原版回退，把心素换成原版的先祖遗物（头环）——
//   那就是"欧洛巴斯之触把心素变成头环"这个 bug 的成因。
[RegisterTouchOfOrobasRefinement(typeof(lihuowang2Relic_YijiXinsu))]
public sealed class lihuowang2Relic_Xinsu : ModRelicTemplate
{
    // 本场战斗内累计抽到的疑虑数量，满3张后清零。跨回合累计，不跨战斗。
    private int _doubtDrawnCount;

    // 计数器的存档/同步出口。遗物是引擎里唯一支持 [SavedProperty] 的模型
    // （引擎自己的 49 个用法全是遗物），而且这项数据会同时出现在两个通道里：
    //  1. 本局存档（SerializableRelic.Props）；
    //  2. 多人战斗快照（NetFullCombatState.FromRun 里 relic.ToSerializable()）——
    //     断线重连的客户端靠它把计数恢复成和主机一致。没有它的话，重连方会从 0 重新累计，
    //     攒满 3 张疑虑触发疯癫的时机就和队友不同（轻则行为不一致，重则校验和不同步）。
    // 跨战斗不残留：BeforeCombatStart / AfterCombatEnd 仍会把它清零。
    [SavedProperty]
    public int DoubtDrawnCount
    {
        get => _doubtDrawnCount;
        private set
        {
            _doubtDrawnCount = value;
            UpdateCounterDisplay();
        }
    }

    // 稀有度。
    public override RelicRarity Rarity => RelicRarity.Starter;

    // 不进商店：商店生成遗物时会按这个属性过滤
    // （MerchantRelicEntry.FillSlot 里的 r.IsAllowedInShops），默认 true。
    public override bool IsAllowedInShops => false;

    // 「视为心素」的统一判定入口：携带「心素」或它的先祖版本「一炁·心素」都算心素。
    // 所有需要心素的门禁都走这里（目前是 替身人皮 牌 lihuowang2XinSuSkin 的 IsPlayable），
    // 以后再加心素系遗物也只需要在这一个方法里补一行，不用去改每一张心素牌。
    public static bool HasXinsu(Player player)
        => player.GetRelic<lihuowang2Relic_Xinsu>() != null
           || player.GetRelic<lihuowang2Relic_YijiXinsu>() != null;

    // 计数器：只在积累了疑虑后才显示，默认（0）不显示
    public override bool ShowCounter => DoubtDrawnCount > 0;

    // 计数器显示的数字，与累计的疑虑数量一致
    public override int DisplayAmount => DoubtDrawnCount;

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
        // 多人：Hook.PlayerTurnStart 会对「每个玩家的回合开始」把场上所有遗物都通知一遍，
        // 所以必须自己判断这次轮到的玩家是不是遗物持有者，否则会把疑虑塞进队友手里。
        if (player != Owner)
        {
            await base.AfterPlayerTurnStartEarly(choiceContext, player);
            return;
        }

        await CardPileCmd.AddToCombatAndPreview<Doubt>(Owner.Creature, PileType.Hand, 1, Owner);
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
        DoubtDrawnCount = 0;
        await base.BeforeCombatStart();
    }

    //这里写方法，每抽到3张疑虑，获得Crazypower的buff
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        // 只统计真正抽到手的疑虑，而且必须是自己的（same reason：队友抽牌也会通知到本遗物）
        if (card is not Doubt || card.Owner != Owner) return;

        await CountDoubtAndTryTriggerCrazy(choiceContext);
    }

    // 累计1张疑虑，满3张时施加疯癫并清零
    private async Task CountDoubtAndTryTriggerCrazy(PlayerChoiceContext choiceContext)
    {
        // 走属性而不是字段：setter 里会顺手刷新计数器显示
        DoubtDrawnCount++;

        if (DoubtDrawnCount < 3) return;

        DoubtDrawnCount = 0;
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
        DoubtDrawnCount = 0;
        await base.AfterCombatEnd(room);
    }

    // 塔1彩蛋：进入最终胜利房间时播放「成仙」BGM（对应 Xinsu.java 的 TrueVictoryRoom 检查）
    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        if (room.GetType().Name.IndexOf("Victory", System.StringComparison.OrdinalIgnoreCase) >= 0)
            Lihuowang2MusicUtil.PlayVictoryMusic("chengxian.mp3");

        await base.AfterRoomEntered(room);
    }

}