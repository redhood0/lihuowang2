using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 朽木如意（稀有）：第 3 个回合开始时，随机击晕一个敌人（一场战斗只触发一次）。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_XiuMuRuYi : ModRelicTemplate
{
    // 稀有度
    public override RelicRarity Rarity => RelicRarity.Rare;

    // 触发回合（第 3 个玩家回合开始时）
    private const int TriggerTurn = 3;

    // 本场战斗是否已经触发过（TurnNumber 只会往上走，这里再兜一层，避免"额外回合"之类的机制重复触发）
    private bool _triggered;

    // 图标：images/relics/lihuowang2Relic_XiuMuRuYi.png（三个尺寸先指向同一张图；
    // 缺图时引擎按原版规则回落占位图，和卡牌一样）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 悬停：描述里的「击晕」。
    // 与「定身符」同一套写法：官方 Whistle 用的 StunIntent.GetStaticHoverTip()
    // （文案取 intents 表的 STUN.title/description，图标走常驻 intent_atlas，不要在别处自己取图）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [StunIntent.GetStaticHoverTip()];

    // 每场战斗都要重新计一次，所以战斗开始时把标记清掉
    public override Task BeforeCombatStart()
    {
        _triggered = false;
        return base.BeforeCombatStart();
    }

    // 回合开始：轮到遗物持有者的第 3 个回合时，随机击晕一个敌人
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人：Hook.PlayerTurnStart 会把「每个玩家的回合开始」通知给场上所有遗物，
        // 所以必须先筛出"这次轮到的就是遗物持有者"，否则会替队友触发。
        if (player != Owner || _triggered || Owner.PlayerCombatState?.TurnNumber != TriggerTurn)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        _triggered = true;

        // 随机目标必须走引擎的确定性随机流（CombatTargets = 战斗内随机选目标用），
        // 否则联机两边抽到的敌人不同，会直接触发状态校验分歧。
        Creature? target = Owner.RunState.Rng.CombatTargets
            .NextItem(Owner.Creature.CombatState!.HittableEnemies);

        if (target != null)
        {
            Flash();
            await CreatureCmd.Stun(target);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }
}
