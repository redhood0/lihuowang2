using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 李岁公主：本角色的专属「先祖」卡（CardRarity.Ancient）。
// 参考官方战士的先祖卡「破击」（Break）：1 费 / 攻击 / 单体 —— 效果暂时为空，等设计好再填。
// 它不进随机奖励池（引擎在 CardFactory 里就把 Ancient / Event 稀有度排除了），
// 获得途径是官方遗物「古老牙齿」（ArchaicTooth）：拿到那颗牙时，
// 卡组里的初始卡「黑太岁」会被超越成这张牌（映射注册见 Lihuowang2Heitaisui 上的
// [RegisterArchaicToothTranscendence]）。
[RegisterCard(typeof(lihuowang2CardPool))]
public sealed class lihuowang2LisuiGongzhu : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Ancient;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2LisuiGongzhu.png（缺失时用占位图）。
    // 先祖卡专用的边框 / 文本底 / 横幅交给引擎按稀有度处理，这里不覆盖。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2LisuiGongzhu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // TODO: 效果待定（先祖卡的效果还没设计），先做成"打出去什么都不发生"的空模板。
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => Task.CompletedTask;

    // TODO: 升级效果待定。
    protected override void OnUpgrade()
    {
    }
}
