using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using lihuowang2.Tags;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianWuxing : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 3;
    // 卡牌类型（能力牌：打出后给自己挂持续效果）
    private const CardType type = CardType.Power;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Rare;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 「死亡回合数」变量名，本地化里用 {Turns:diff()} 显示；升级后 2 → 3
    private const string TurnsVarName = "Turns";

    // 大千录 tag
    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu
    ];

    // 悬停时展示自己挂上的持续效果说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<Lihuowang2DaqianWuxingPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianWuxing.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：Turns = 死亡倒计时回合数（2，升级 +1 → 3）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar(TurnsVarName, 2m)
    ];

    // 关键字：消耗（打出后进入消耗堆）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust
    ];

    public lihuowang2DaqianWuxing() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：给自己施加「置闰五行」。重复打出会刷新倒计时回合数。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal turns = DynamicVars[TurnsVarName].BaseValue;

        // 已有该效果：把倒计时刷新到本牌的回合数（而不是叠加）
        Lihuowang2DaqianWuxingPower? existing =
            Owner.Creature.GetPower<Lihuowang2DaqianWuxingPower>();
        if (existing != null)
        {
            await PowerCmd.ModifyAmount(choiceContext, existing, turns - existing.Amount, Owner.Creature, this);
            return;
        }

        // 首次打出：施加置闰五行，Amount = 倒计时回合数
        await PowerCmd.Apply<Lihuowang2DaqianWuxingPower>(choiceContext, Owner.Creature,
            turns, Owner.Creature, this);
    }

    // 升级：死亡倒计时 2 → 3 回合
    protected override void OnUpgrade()
    {
        DynamicVars[TurnsVarName].UpgradeValueBy(1);
    }
}
