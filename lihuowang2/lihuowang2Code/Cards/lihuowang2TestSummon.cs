using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Characters;
using lihuowang2.Minions;
using MinionLib.Commands;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 测试用召唤牌：1 费技能，召唤随从「曹操」（生命 10）。
// 正式版如果不需要进卡池，可以把这个 [RegisterCard] 去掉或换到别的池。
// [RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2TestSummon : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2TestSummon.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2TestSummon() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 随从血量由 TestMinion 自己的 MinInitialHp/MaxInitialHp 决定（10），
        // 这里只指定站位与来源。
        await MinionCmd.AddMinion<TestMinion>(choiceContext, Owner, new MinionSummonOptions(
            Source: this,
            Position: MinionPosition.Front));
    }
}
