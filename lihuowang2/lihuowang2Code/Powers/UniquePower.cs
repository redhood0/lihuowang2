using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 唯一（随从「彭龙腾」身上的标记能力）：
// 场上已经有这只单位时，再打出召唤牌不会再召唤第二只，而是给已有的那一只「增加对应血量」
// （加血量 = 召唤牌上的血量参数 MinionHp，见 PengLongTeng.ReinforceIfExisting）。
//
// 本能力只负责「显示 + 说明」，判定写在召唤流程里 —— 官方「奥斯提」也是这么分的：
// 机制在 OstyCmd.Summon 里（"If the specified creature already owns an instance of Osty,
// raise Osty's max HP by the specified number instead."），而不是写成一个能力。
[RegisterPower]
public class UniquePower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // 不吃层数：它只是"这只单位是唯一的"这个标记，图标上不需要数字。
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标：暂时借用官方能力「爪牙」（MegaCrit.Sts2.Core.Models.Powers.MinionPower）的图标。
    // 直接从官方模型拿路径，不手写/猜资源名：
    //   IconPath            = 官方图集精灵（引擎 PowerModel.PackedIconPath，即图标行用的小图标）
    //   ResolvedBigIconPath = 官方大图（powers/minion_power.png；缺失时引擎内部会自己回落到占位图）
    // 等专属图标画好了，换回模组自己的图即可：
    //   new PowerAssetProfile(IconPath: $"{Entry.ResPath}/images/powers/unique.png",
    //                         BigIconPath: $"{Entry.ResPath}/images/powers/unique.png")
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel minion = ModelDb.Power<MinionPower>();
            return new PowerAssetProfile(
                IconPath: minion.IconPath,
                BigIconPath: minion.ResolvedBigIconPath);
        }
    }
}
