using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 触手·延伸（罕见）：需要黑太岁才能打出；获得 Stretch 点「拉伸」。
// 若被消耗（任何来源），回复 Heal 点生命。
// 卡图：images/cards/lihuowang2YanShen.png（类名 = 图片名）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2YanShen : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：打出条件里的「黑太岁」，以及描述里点名的官方能力「拉伸」。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<Lihuowang2HeitaisuiPower>(),
        HoverTipFactory.FromPower<StretchPower>()
    ];

    // Stretch = 获得的拉伸层数（9，升级 +3 → 12）；Heal = 被消耗时回复的生命（3，升级 +1 → 4）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Stretch", 9m),
        new DynamicVar("Heal", 3m)
    ];

    public lihuowang2YanShen() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 需要黑太岁才能打出（与其它触手牌一致；图鉴/预览不做限制）
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
                return true;
            return Owner.Creature.GetPower<Lihuowang2HeitaisuiPower>() != null;
        }
    }

    // 获得拉伸：官方 StretchPower —— 本回合下一张获得格挡的牌，格挡 +层数。
    // 本牌自身不获得格挡，所以不会被它自己消耗掉。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StretchPower>(choiceContext, Owner.Creature,
            DynamicVars["Stretch"].BaseValue, Owner.Creature, this);
    }

    // 被消耗时回复生命（任何来源的消耗都算）。
    // 不要在别处（例如黑太岁能力里）再判一次，否则会重复回血。
    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal)
    {
        if (card != this)
            return;

        await CreatureCmd.Heal(Owner.Creature, DynamicVars["Heal"].BaseValue);
        await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
    }

    // 升级：拉伸 9 → 12；被消耗时回血 3 → 4
    protected override void OnUpgrade()
    {
        DynamicVars["Stretch"].UpgradeValueBy(3);
        DynamicVars["Heal"].UpgradeValueBy(1);
    }
}
