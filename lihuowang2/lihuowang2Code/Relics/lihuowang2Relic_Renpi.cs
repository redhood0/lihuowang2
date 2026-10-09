using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 替身人皮（遗物）：打出「替身人皮」卡时获得。
// 濒死时挡下一次致命伤害，并回复最大生命的 50%（参考官方蜥蜴尾巴）；
// 然后**人皮烧掉**——遗物本身消失（一次性），同时之前从卡组里暂时移走的那张
// 「替身人皮」回到卡组，可以再次打出 → 再次获得一件新的人皮。
//
// 与旧版的区别：旧版是"用卡给已破损的人皮充能"（遗物常驻、靠 WasUsed/Disabled 表示破损），
// 现在改成"打牌时把卡从卡组拿走、换一件新人皮；人皮生效后卡回到卡组、遗物消失"。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Renpi : ModRelicTemplate
{
    // 被"保管"在这里的那张卡：卡从卡组移走时的完整数据（含升级等级、附魔、floor 等）。
    //
    // 为什么存 SerializableCard 而不是直接存 CardModel：
    //  1. 遗物的 [SavedProperty] 是它唯一的存档/同步出口（本局存档 + 多人战斗快照双通道），
    //     存对象引用既进不了档，断线重连后也拿不到；SerializableCard 是引擎既有的可序列化卡牌形态
    //     （SavedProperties 里专门有一栏 List<SavedProperty<SerializableCard>> 就是给这种用法）。
    //  2. 重建时用 RunState.LoadCard(...) 还原 → 升级等级、附魔等原样回到卡上。
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public SerializableCard? StoredCard { get; private set; }

    // 只有心素专供卡能获得，不进普通掉落池
    public override RelicRarity Rarity => RelicRarity.Event;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 由「替身人皮」卡在打出时调用：把"刚从卡组里拿下来的那张牌"交给本遗物保管。
    // 卡牌先自己 ToSerializable()（移除前取，移除后卡就 HasBeenRemovedFromState 了），再交进来。
    public void StoreCard(SerializableCard card)
    {
        StoredCard = card;
    }

    // 未触发前一直拦截致命死亡（触发后遗物就被移除了，所以这里永远返回 false）。
    // 返回 true = 放行死亡（队友/敌人的死亡不归管）。
    public override bool ShouldDieLate(Creature creature)
        => creature != Owner.Creature;

    // 拦死成功：回 50% 最大生命 → 把卡还回卡组 → 人皮消失
    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();

        // 1) 先回血（本钩子触发时玩家血量正是 0，先活过来更自然，也避免后续操作在"死亡中"状态下做）
        decimal healAmount = Math.Max(1m, (decimal)creature.MaxHp * 0.5m);
        await CreatureCmd.Heal(creature, healAmount);

        // 2) 把暂存的卡牌还回卡组
        SerializableCard? stored = StoredCard;
        StoredCard = null;   // 先清空：即使下面出错也不会重复还牌

        if (stored != null && Owner != null)
        {
            // LoadCard = CardModel.FromSerializable + RunState.AddCard（把卡重新登记进本局），
            // 这时牌还没在任何牌堆里，再放进卡组牌堆。
            //
            // ⚠ 这里刻意不用 CardPileCmd.Add：它内部有 `creature.IsDead` 守卫，而本钩子触发时
            //    玩家血量正好是 0（IsDead == CurrentHp <= 0）→ 会被静默拒绝（success=false，牌就没了）。
            //    引擎自己在开局给诅咒上卡组时也是直接 AddInternal（AscensionLevel.ApplyEffectsTo）。
            //    走 AddInternal 非 silent 重载 → 会发 CardAdded / ContentsChanged，卡组界面照常刷新。
            CardModel card = Owner.RunState.LoadCard(stored, Owner);
            Owner.Deck.AddInternal(card, -1);
        }

        // 3) 人皮烧掉：遗物从遗物栏消失（一次性）。放在最后，因为这会把自己从钩子总线上摘掉。
        await RelicCmd.Remove(this);
    }
}
