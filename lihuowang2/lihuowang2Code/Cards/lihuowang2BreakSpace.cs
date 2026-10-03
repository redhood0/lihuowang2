using System.Collections.Generic;
using System.Linq;
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
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Void = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace lihuowang2.Cards;

// 破碎虚空斩：清空所有敌人的格挡并对其造成伤害，随后弃牌堆放入 1 张虚空。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2BreakSpace : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的「虚空」。
    // 虚空是引擎自带的卡牌（MegaCrit.Sts2.Core.Models.Cards.Void，本文件顶部已 using 别名），
    // 文案/卡面直接取官方，不需要自己写。
    // 用 FromCard<T>() 取单条提示，与「回娘家」给官方 Regret、「心素」给官方 Doubt 挂 hover 的写法一致。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<Void>()];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(33m, ValueProp.Move)
    ];

    public lihuowang2BreakSpace() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;

        // 1. 清空格挡（用快照副本遍历，防止敌人中途被移除导致枚举异常）
        Creature[] enemies = Owner.Creature.CombatState!.Enemies.ToArray();
        foreach (Creature enemy in enemies)
        {
            if (enemy.Block > 0)
                await CreatureCmd.LoseBlock(choiceContext, enemy, enemy.Block, Owner.Creature);
        }

        // 2. 对全体敌人造成伤害（TargetingAllOpponents 内部处理敌人阵亡；
        //    群攻的命中特效默认只播一次、落在敌方一侧，不会每只敌人都叠一片）
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(Owner.Creature.CombatState!)
            .WithHitFx("vfx/vfx_starry_impact")   // 命中特效：虚空星辉冲击（贴合「破碎虚空」）
            .Execute(choiceContext);

        // 3. 弃牌堆放入 1 张虚空
        if (player != null)
            await CardPileCmd.AddToCombatAndPreview<Void>(Owner.Creature, PileType.Discard, 1, player);
    }

    // 升级：伤害 30 → 40
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(11);
    }
}
