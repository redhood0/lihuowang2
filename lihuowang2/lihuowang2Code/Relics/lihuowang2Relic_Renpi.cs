using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 替身人皮：濒死时挡下一次致命伤害，并回复最大生命的 50%（参考官方蜥蜴尾巴）。
// 破损后可用「替身人皮」卡再次充能。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Renpi : ModRelicTemplate
{
    private bool _wasUsed;

    // 只有心素专供卡能获得/充能，不进普通掉落池
    public override RelicRarity Rarity => RelicRarity.Event;

    // 已破损
    public override bool IsUsedUp => _wasUsed;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 未破损时拦截致命死亡
    public override bool ShouldDieLate(Creature creature)
    {
        if (creature != Owner.Creature)
            return true;
        return _wasUsed;
    }

    // 拦死成功：破损 + 回 50% 最大生命
    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();
        _wasUsed = true;
        Status = RelicStatus.Disabled;

        decimal healAmount = Math.Max(1m, (decimal)creature.MaxHp * 0.5m);
        await CreatureCmd.Heal(creature, healAmount);
    }

    // 用「替身人皮」卡再次充能
    public void Recharge()
    {
        _wasUsed = false;
        Status = RelicStatus.Active;
    }
}
