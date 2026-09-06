using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 搜刮：攻击并捡钱。有煞气时拿的 = 煞气层数（升级翻倍）；没有煞气时先攒一层再拿钱。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Loot : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Damage = 伤害（4）；Magic = 无煞气时的初始金币/煞气（1，升级 +1）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Magic", 1m)
    ];

    public lihuowang2Loot() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        ShaQiPower? shaqi = Owner.Creature.GetPower<ShaQiPower>();
        decimal gold;
        if (shaqi != null)
        {
            gold = shaqi.Amount;
            if (IsUpgraded)
                gold *= 2m;
        }
        else
        {
            gold = DynamicVars["Magic"].BaseValue;
            await PowerCmd.Apply<ShaQiPower>(choiceContext, Owner.Creature, gold, Owner.Creature, this);
        }

        await PlayerCmd.GainGold(gold, player);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    // 升级：初始金币/煞气 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}
