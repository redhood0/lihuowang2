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
