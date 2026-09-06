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

    // 自定义图标（先沿用现成 Heitaisui 32/84，之后有正式图直接替换路径即可）
    public PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/Heitaisui32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/Heitaisui84.png");

    public string? CustomIconPath => AssetProfile.IconPath;

    public string? CustomBigIconPath => AssetProfile.BigIconPath;
}
