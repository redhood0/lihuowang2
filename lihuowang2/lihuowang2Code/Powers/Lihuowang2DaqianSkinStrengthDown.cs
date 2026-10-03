using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace lihuowang2.Powers;

// 官方「临时力量下降」子类（与 ManglePower 同款机制）：
// IsPositive = false → 引擎自动 Apply StrengthPower(-amount)，目标回合结束时 +amount 补回。
// 通过 IModPowerAssetOverrides 指定图标（先用 Heitaisui 图占位，可随时换）。
[RegisterPower]
public class Lihuowang2DaqianSkinStrengthDown : TemporaryStrengthPower, IModPowerAssetOverrides
{
    // 负值（力量下降）
    protected override bool IsPositive => false;

    // 来源模型（用于状态栏标题等，指向「大千录·剥皮」这张牌）
    public override AbstractModel OriginModel => ModelDb.Card<lihuowang2DaqianSkin>();

    // 图标：复用官方「尖啸」（PiercingWail / 刺耳尖啸）那条"临时力量下降"的官方图标。
    // 写法与 UniquePower 借用官方「爪牙」(MinionPower) 图标一致 —— 直接从官方模型上取路径，不手写/猜资源名：
    //   IconPath            = 官方图集精灵（引擎 PowerModel.PackedIconPath，= 图标行里的小图标）
    //   ResolvedBigIconPath = 官方大图（powers/piercing_wail_power.png；缺失时引擎内部会自己回落到占位图）
    // 等专属图标画好，换回模组自己的图即可：
    //   new PowerAssetProfile(IconPath: $"{Entry.ResPath}/images/powers/daqianSkinStrengthDown.png",
    //                         BigIconPath: $"{Entry.ResPath}/images/powers/daqianSkinStrengthDown.png")
    public PowerAssetProfile AssetProfile
    {
        get
        {
            PowerModel wail = ModelDb.Power<PiercingWailPower>();
            return new PowerAssetProfile(
                IconPath: wail.IconPath,
                BigIconPath: wail.ResolvedBigIconPath);
        }
    }

    public string? CustomIconPath => AssetProfile.IconPath;

    public string? CustomBigIconPath => AssetProfile.BigIconPath;
}
