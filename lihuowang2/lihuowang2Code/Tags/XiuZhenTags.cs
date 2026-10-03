using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace lihuowang2.Tags;

// 修真 tag：修真系卡牌（召唤/牺牲随从换资源的那些，例如「修真·秋吃饱」「修真·彭龙腾」）。
// 用法与 DaqianTags 完全一致：
//   · [RegisterOwnedCardTag] 是"自动注册"的关键 —— RitsuLib 启动时会扫到它并登记这张 tag；
//   · 静态字段里那串 = 本模组的带前缀 tag id（ModContentRegistry.GetQualifiedCardTagId(模组id, 名字)），
//     再用 GetModCardTag() 转成引擎要的 CardTag。
// 牌那边用 `protected override HashSet<CardTag> CanonicalTags` 挂上即可（不会再显示在卡面上，
// tag 是纯机制标记，所以不需要本地化文案 —— 参考 DaqianTags.DaqianLu）。
[RegisterOwnedCardTag(nameof(XiuZhen))]
public class XiuZhenTags
{
    public static readonly CardTag XiuZhen =
        ModContentRegistry.GetQualifiedCardTagId(Entry.ModId, nameof(XiuZhen)).GetModCardTag();
}
