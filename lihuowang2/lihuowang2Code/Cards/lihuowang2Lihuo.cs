using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
public class lihuowang2Lihuo : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AllEnemies：全体敌人）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 每次移除的点燃层数，本地化用 {Layers:diff()} 显示；升级 1 → 2
    private const string LayersVarName = "Layers";

    // 悬停提示：点燃是什么
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<DianranPower>()];

    // 卡图资源。对应 lihuowang2/images/cards/lihuowang2Lihuo.png（缺失时用占位图）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 卡牌基础数值：Layers = 移除层数（1，升级 +1 → 2）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar(LayersVarName, 1m)
    ];

    public lihuowang2Lihuo() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出：从每个敌人身上移除至多 Layers 层点燃（每移除 1 层抽 1 张牌）；
    // 若某敌人的点燃因此清零，则重新点燃 1 层。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int removeLayers = (int)DynamicVars[LayersVarName].BaseValue;
        int totalRemoved = 0;

        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        foreach (Creature enemy in enemies)
        {
            if (enemy.IsDead)
                continue;

            DianranPower? ignite = enemy.GetPower<DianranPower>();
            if (ignite == null)
            {
                // 没有点燃就补 1 层
                await DianranPower.ApplyIgnite(choiceContext, enemy, 1m, Owner.Creature, this);
                continue;
            }

            if (ignite.Amount <= removeLayers)
            {
                // 会移除干净：消耗全部层数后，重新点燃 1 层
                totalRemoved += (int)ignite.Amount;
                await PowerCmd.Remove(ignite);
                await DianranPower.ApplyIgnite(choiceContext, enemy, 1m, Owner.Creature, this);
            }
            else
            {
                totalRemoved += removeLayers;
                await PowerCmd.ModifyAmount(choiceContext, ignite, -removeLayers, Owner.Creature, this);
            }
        }

        // 每移除 1 层抽 1 张牌
        if (totalRemoved > 0)
            await CardPileCmd.Draw(choiceContext, totalRemoved, Owner.Creature.Player!);
    }

    // 升级：移除层数 1 → 2
    protected override void OnUpgrade()
    {
        DynamicVars[LayersVarName].UpgradeValueBy(1);
    }
}
