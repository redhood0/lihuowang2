using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册，加入 lihuowang2CardPool，可在奖励中出现。
[RegisterCard(typeof(lihuowang2CardPool))]
[RegisterCharacterStarterCard(typeof(lihuowang2Character), 1)]
// 官方遗物「古老牙齿」（ArchaicTooth）的效果是"把初始卡超越成先祖卡"（战士：重击 Bash → 破击 Break）。
// 这里把本角色的初始卡 黑太岁 注册成 李岁公主：拿到古老牙齿时，RitsuLib 会让那张遗物
// 在卡组里找到黑太岁并转化成李岁公主（升级状态、附魔都会由引擎一起带过去）。
[RegisterArchaicToothTranscendence(typeof(lihuowang2LisuiGongzhu))]
public sealed class Lihuowang2Heitaisui : ModCardTemplate
{
    // 基础耗能。
    private const int BaseEnergyCost = 1;

    // 卡牌类型（能力牌）。
    private const CardType CardKind = CardType.Power;

    // 卡牌稀有度。
    private const CardRarity CardRarityValue = CardRarity.Uncommon;

    // 目标类型（Self 表示自己）。
    private const TargetType CardTarget = TargetType.Self;

    // 是否在卡牌图鉴中显示。
    private const bool ShowInCardLibrary = true;

    // 卡图资源。文件名对应 lihuowang2/images/cards/BlackTaiSui.png。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/BlackTaiSui.png");

    // 悬停时展示施加的持续能力说明。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<Lihuowang2HeitaisuiPower>()];

    public Lihuowang2Heitaisui() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 打出时：给 Owner 施加持续能力（能力牌打出自会进入消耗堆），并把战斗形象换成「黑太岁」形态。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<Lihuowang2HeitaisuiPower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 形象变化：lhw_character.png → lhw_trans01.png。
        // 只影响本地战斗场景的那具身体；新战斗会重新实例化场景，能力被移除时也会还原（见 Power.AfterRemoved）。
        Lihuowang2VisualUtil.SetBodyTexture(Owner.Creature, Lihuowang2VisualUtil.HeitaisuiBodyPath);
    }

    // 升级后：获得「固有」（开局在手）。
    protected override void OnUpgrade()
    {
        CardCmd.ApplyKeyword(this, CardKeyword.Innate);
    }
}
