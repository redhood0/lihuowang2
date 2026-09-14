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
public class lihuowang2DaqianYoufutongxiang : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型（能力牌）
    private const CardType type = CardType.Power;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（Self：只对自己）
    private const TargetType targetType = TargetType.Self;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 伤害倍率变量名，本地化里用 {Share:diff()} 显示；升级 2 → 3
    private const string ShareVarName = "Share";

    // 悬停时展示有福同享效果说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<YoufutongxiangPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2DaqianYoufutongxiang.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：Share = 反伤倍率（2，升级 +1 → 3）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar(ShareVarName, 2m)
    ];

    public lihuowang2DaqianYoufutongxiang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 大千录 tag
    protected override HashSet<CardTag> CanonicalTags => [
        DaqianTags.DaqianLu
    ];

    // 打出：给自己挂「有福同享」层数（倍率）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<YoufutongxiangPower>(choiceContext, Owner.Creature,
            DynamicVars[ShareVarName].BaseValue, Owner.Creature, this);
    }

    // 升级：倍率 2 → 3
    protected override void OnUpgrade()
    {
        DynamicVars[ShareVarName].UpgradeValueBy(1);
    }
}
