using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 回娘家：点燃 3 层；若敌人点燃 ≥6 层则打出重击；杀死目标时把悔恨加入牌组。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Huiniangjia : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<DianranPower>(),
        HoverTipFactory.FromCard<Regret>()
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Magic = 点燃层数 × 伤害倍率（8，升级 +2）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Magic", 8m)
    ];

    public lihuowang2Huiniangjia() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature target = cardPlay.Target!;
        Player? player = Owner.Creature.Player;

        

        // 1. 点燃 3 层（黑手遗物加成已由 DianranPower 内部处理）
        await DianranPower.ApplyIgnite(choiceContext, [target], 3m, Owner.Creature, this);

        // 2. 点燃 ≥6 层：打出一记重击（点燃层数 × Magic）
        DianranPower? ignite = target.GetPower<DianranPower>();
        if (ignite == null || ignite.Amount < 6m)
            return;

        decimal damage = ignite.Amount * DynamicVars["Magic"].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);
        
        // BGM：回娘家（离火）
        Lihuowang2MusicUtil.PlayCardMusic("lihuo.mp3");
        
        // 3. 若因此击杀：把一张「悔恨」加入牌组
        if (target.IsDead && player != null)
            await CardPileCmd.AddCursesToDeck([ModelDb.Card<Regret>()], player);
    }

    // 升级：倍率 8 → 10
    protected override void OnUpgrade()
    {
        DynamicVars["Magic"].UpgradeValueBy(2);
    }
}
