using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 慈母心斋：在你的回合内主动失去生命值时，获得等量的格挡。
//
// 写法参考原版遗物「恶魔之舌」（DemonTongue）：
//   · 钩子用 AfterDamageReceived —— 结算之后才拿得到"实际掉的血"（DamageResult.UnblockedDamage
//     在引擎里就是 currentHp - CurrentHp，已被当前生命钳制，所以 999999 那种自伤不会刷出天文数字）；
//   · "回合内"用 CombatState.CurrentSide == 自己那边 判断（DemonTongue 同款）；
//   · 加格挡用 CreatureCmd.GainBlock(...)，ValueProp 传 Unpowered（不受敏捷等加成影响）。
//
// ⚠ 判定全部来自"内容本身"（目标 / 施加者 / 当前行动方），两端会算出同一个值 → 不会状态分歧。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Cimuxinzhai : ModRelicTemplate
{
    // 罕见
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    // 图标：lihuowang2/images/relics/lihuowang2Relic_Cimuxinzhai.png（三处都用同一张）。
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 受到伤害结算后：若这是"自己主动失去的生命"，就按实际失去的量获得格挡。
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // ① 掉血的必须是自己
        if (target != Owner.Creature)
            return;

        // ② "主动"：施加者就是自己（自伤/自残，与「有福同享」同一套判据；敌人打的伤害不算）
        if (dealer != Owner.Creature)
            return;

        // ③ "在回合内"：当前行动方就是自己那一边
        if (Owner.Creature.CombatState?.CurrentSide != Owner.Creature.Side)
            return;

        // ④ 确实失去了生命（被格挡挡下、或伤害为 0 的情况不算）
        if (result.UnblockedDamage <= 0)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, result.UnblockedDamage, ValueProp.Unpowered, null);
    }
}
