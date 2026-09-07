using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace lihuowang2.Tags;

[RegisterOwnedCardTag(nameof(DaqianLu))]   // ← 关键：在类上挂这个特性，RitsuLib 启动时会自动扫描注册
public class DaqianTags
{
    public static readonly CardTag DaqianLu =
        ModContentRegistry.GetQualifiedCardTagId(Entry.ModId, nameof(DaqianLu)).GetModCardTag();
}