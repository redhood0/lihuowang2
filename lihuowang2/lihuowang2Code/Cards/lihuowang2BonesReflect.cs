using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 骨刺反转：有荆棘则荆棘翻倍；没有则清空格挡，换成其一半层数的荆棘。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BonesReflect : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 初始为消耗牌，升级后不再消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2BonesReflect() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;

        ThornsPower? thorns = self.GetPower<ThornsPower>();
        if (thorns != null && thorns.Amount > 0m)
        {
            // 荆棘翻倍：再叠加当前层数
            await PowerCmd.Apply<ThornsPower>(choiceContext, self, thorns.Amount, self, this);
            return;
        }

        // 没有荆棘：清空格挡，换取一半层数的荆棘
        int block = self.Block;
        self.Block = 0;
        int thornsAmount = block / 2;
        if (thornsAmount > 0)
            await PowerCmd.Apply<ThornsPower>(choiceContext, self, thornsAmount, self, this);
    }

    // 升级：不再消耗
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
