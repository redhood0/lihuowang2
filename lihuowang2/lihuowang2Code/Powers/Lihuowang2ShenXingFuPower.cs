using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using lihuowang2.Cards;

namespace lihuowang2.Powers;

// 神行符的「临时敏捷」。
//
// 原版的 TemporaryDexterityPower 是**抽象基类**（源码注释：We never instantiate this directly），
// 每张给临时敏捷的牌都要有自己继承它的具体能力（例如原版 Anticipate → AnticipatePower，
// 里面只提供 OriginModel）。直接 PowerCmd.Apply<TemporaryDexterityPower> 会在
// ModelDb.Power<T>() 里取不到模型实例（KeyNotFoundException），卡牌结算永远不会完成 ——
// 表现为打出这张牌就卡死。
[RegisterPower]
public class Lihuowang2ShenXingFuPower : TemporaryDexterityPower, IModPowerAssetOverrides
{
    // 施加者：神行符（临时能力的标题取自这个模型，所以图标行会显示「神行符」）
    public override AbstractModel OriginModel => ModelDb.Card<lihuowang2ShenXingFu>();

    // 图标先借用原版「敏捷」的贴图（模组暂时没有自己的临时敏捷美术资源）；
    // 万一取不到就返回空配置，让引擎按默认（缺失图标占位）处理，不会崩。
    // public PowerAssetProfile AssetProfile
    // {
    //     get
    //     {
    //         try
    //         {
    //             DexterityPower dexterity = ModelDb.Power<DexterityPower>();
    //             return new PowerAssetProfile(dexterity.PackedIconPath, dexterity.ResolvedBigIconPath);
    //         }
    //         catch
    //         {
    //             return PowerAssetProfile.Empty;
    //         }
    //     }
    // }
    
    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public  PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/fuzhuanlupower32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/fuzhuanlupower84.png");

    

    public string? CustomIconPath => AssetProfile.IconPath;

    public string? CustomBigIconPath => AssetProfile.BigIconPath;
}
