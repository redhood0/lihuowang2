using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 修真（随从「秋吃饱」身上的能力）。
//
// 看起来是两件事，其实是一件事：
//  1. 随从存活期间，召唤者的「最大能量」-1。
//     引擎的最大能量不是存档字段，而是钩子汇总出来的（Hook.ModifyMaxEnergy 收集所有模型的结果），
//     模组的遗物「火镰」就是靠同一个钩子 +1。
//  2. 随从死亡时，本 power 会被引擎一并移除（ShouldPowerBeRemovedAfterOwnerDeath 默认 true），
//     那 -1 自然消失 —— 也就是「死亡时，召唤者恢复 1 点最大能量」。
// 所以不需要额外的死亡钩子：还能量是 power 消失的自然结果，也不会出现重复归还。
[RegisterPower]
public class XiuZhenPower : ModPowerTemplate
{
    // 对召唤者来说是代价（最大能量 -层数），所以标成 Debuff
    public override PowerType Type => PowerType.Debuff;

    // 计数器：层数 = 被扣住的「最大能量」点数。
    // 1 层 → 召唤者最大能量 -1，死亡时归还 1；
    // 2 层 → -2，死亡时归还 2（Amount 既是扣减量也是归还量，见 ModifyMaxEnergy）。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标沿用模组命名习惯（图片缺失时引擎会用占位图）
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/xiuzhen84.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/xiuzhen84.png");

    // 召唤者的最大能量 -层数。
    // Owner = 随从自己；Owner.PetOwner = 召唤它的那个玩家（不是宠物时为 null）。
    // 用 base.Amount 而不是写死 1：层数即扣减量，随从死亡时这整份扣减随 power 一起消失，
    // 于是「1 层归还 1 点、2 层归还 2 点」是同一套逻辑。
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner.PetOwner)
            return amount;

        return amount - base.Amount;
    }
}
