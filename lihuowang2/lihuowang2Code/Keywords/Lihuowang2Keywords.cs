using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace lihuowang2.Keywords;

// 模组自定义关键词的声明处（Entry.Initialize() 里注册程序集后由自动注册扫描处理）。
// 注册发生在模型初始化之前，注册表随后冻结；限定 ID 由 RitsuLib 按 "{模组ID}_KEYWORD_{词干}" 生成，
// 这里即 LIHUOWANG2_KEYWORD_FORESEE，文本取本地化表 card_keywords 的
// LIHUOWANG2_KEYWORD_FORESEE.title / LIHUOWANG2_KEYWORD_FORESEE.description。
[RegisterOwnedCardKeyword("foresee")]
public sealed class Lihuowang2ForeseeKeyword
{
    // 「预见」的关键字值。卡牌把它写进 CanonicalKeywords 后，
    // 悬停时引擎会像原版关键词（消耗/固有）那样自动带上这条说明。
    public static CardKeyword Value => ModKeywordRegistry.GetCardKeyword("LIHUOWANG2_KEYWORD_FORESEE");
}

// 「耐久」：耐久牌每次打出后耐久 -1；耐久归零时这张牌会被「消耗」。
// 机制本体（数值变量 + 自动扣减/消耗）见 Cards/Lihuowang2Durability.cs 的 DurabilityCardTemplate。
// 卡面数字「耐久（X/Y）」由 DurabilityVar 提供（描述里写 {Durability}），这里这个关键字只负责 hover 说明。
[RegisterOwnedCardKeyword("durability")]
public sealed class Lihuowang2DurabilityKeyword
{
    public static CardKeyword Value => ModKeywordRegistry.GetCardKeyword("LIHUOWANG2_KEYWORD_DURABILITY");
}
