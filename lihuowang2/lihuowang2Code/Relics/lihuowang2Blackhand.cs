using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using lihuowang2.Characters;
using MegaCrit.Sts2.Core.Entities.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 黑手：你施加「点燃」时层数 +1。
// 实际加成逻辑在 DianranPower.ApplyIgnite 中按是否拥有此遗物判断。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Blackhand : ModRelicTemplate
{
    // 稀有度
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    // 图片资源（先用占位图，有正式图后替换路径）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/lihuowang2Blackhand.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/lihuowang2Blackhand.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/lihuowang2Blackhand.png");
}
