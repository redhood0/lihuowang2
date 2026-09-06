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
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2MoneySword : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型（攻击）
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AnyEnemy：选择一个敌人）
    private const TargetType targetType = TargetType.AnyEnemy;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 悬停提示：铜钱效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<MoneyPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2MoneySword.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Damage = 每次伤害（12，升级 +2）；GoldCost = 消耗金币（20，升级 -5 → 15）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(12m, ValueProp.Move),
        new DynamicVar("GoldCost", 20m)
    ];

    public lihuowang2MoneySword() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 金币不足时无法打出
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature?.Player == null)
                return true; // 图鉴/预览场景不做限制
            return Owner.Creature.Player.Gold >= DynamicVars["GoldCost"].BaseValue;
        }
    }

    // 打出：消耗金币；每持有一枚铜钱就多打一次，随后获得 1 枚铜钱（上限 5）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        decimal goldCost = DynamicVars["GoldCost"].BaseValue;
        if (player.Gold < goldCost)
            return; // 金币不足：效果不结算

        // 1. 消耗金币（撒币）
        await PlayerCmd.LoseGold(goldCost, player);

        // 2. 结算攻击次数（铜钱层数 +1 次；无铜钱则 1 次）
        MoneyPower? copper = Owner.Creature.GetPower<MoneyPower>();
        int hits = (copper != null) ? (int)copper.Amount + 1 : 1;
        for (int i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target!)
                .Execute(choiceContext);
        }

        // 3. 获得 1 枚铜钱（未达上限）
        await MoneyPower.ApplyMoney(choiceContext, Owner.Creature, Owner.Creature, this);
    }

    // 升级：伤害 12 → 14；消耗金币 20 → 15
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["GoldCost"].UpgradeValueBy(-5);
    }
}
