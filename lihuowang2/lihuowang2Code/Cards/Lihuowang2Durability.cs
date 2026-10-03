using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// ===== 耐久（Durability）机制：可复用的卡牌效果 =====
//
// 规则：
//   · 耐久牌每次「打出」后耐久 -1；
//   · 打完后耐久归零 → 这张牌被「消耗」（进消耗堆，不进弃牌堆）；
//   · 否则一切照常（正常进弃牌堆）。
//
// 卡面数字（"耐久（X/Y）"，X = 剩余、Y = 总）：
//   描述里写 `耐久（{Durability}）`，数字由下面的 DurabilityVar.ToString() 提供，
//   随打出自动更新 —— 和模组里 `{Damage:diff()}` 那些占位符是同一套机制（引擎渲染读 DynamicVar.ToString()，
//   官方 StringVar 也是这么供值的，参考 Relics/lihuowang2Relic_Dice18.cs）。
//
// 用法（本次按需求**不实装到任何卡牌上**，需要时照着继承即可）：
//
//     [RegisterCard(typeof(lihuowang2CardPool))]
//     public class lihuowang2SomeCard : DurabilityCardTemplate
//     {
//         // 总耐久：写在这里（或做成 DynamicVar 供卡面引用，见下）
//         protected override int MaxDurability => 3;
//
//         // 自己的数值变量写这里（不要再覆写 CanonicalVars，否则会丢掉耐久变量）
//         protected override IEnumerable<DynamicVar> ExtraVars => [new DamageVar(6m, ValueProp.Move)];
//
//         public lihuowang2SomeCard()
//             : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, true) { }
//
//         protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
//         {
//             // 效果照常写：耐久 -1 与"归零即消耗"由基类自动处理
//         }
//     }
//
// 卡牌描述（localization/<lang>/cards.json）里记得带上耐久那一行，例如：
//     "...造成{Damage:diff()}点伤害。\n耐久（{Durability}）。"
public sealed class DurabilityVar : DynamicVar
{
    // 卡面占位符名：描述里写 {Durability}
    public const string DefaultName = "Durability";

    public DurabilityVar(int maxDurability, string name = DefaultName)
        : base(name, maxDurability)
    {
        Max = maxDurability;
    }

    // 总耐久（Y）。这是"这张牌一共能用几次"，不随使用变化。
    public int Max { get; }

    // 剩余耐久（X）。存在 BaseValue 里，所以会跟着卡牌实例一起克隆/存档，不会因为复制而丢。
    public int Remaining => (int)BaseValue;

    // 耐久是否已用尽
    public bool IsEmpty => Remaining <= 0;

    // 用掉一次：耐久 -1（不会低于 0）
    public void Consume()
    {
        if (Remaining > 0)
            BaseValue = Remaining - 1;
    }

    // 补满（每场战斗开始时调用）
    public void Refill() => BaseValue = Max;

    // 卡面文本：X/Y —— 描述里的 {Durability} 会渲染成这个字符串
    public override string ToString() => $"{Remaining}/{Max}";
}

// 耐久牌基类：继承它 + 覆写 MaxDurability 就自动获得「耐久」机制
public abstract class DurabilityCardTemplate : ModCardTemplate
{
    protected DurabilityCardTemplate(int energyCost, CardType type, CardRarity rarity,
        TargetType targetType, bool shouldShowInCardLibrary)
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 总耐久次数（子类覆写；想把它做成可调数值就自己在 ExtraVars 里加变量）
    protected abstract int MaxDurability { get; }

    // 卡面上的耐久变量（描述里用 {Durability} 引用）
    protected DurabilityVar Durability => (DurabilityVar)DynamicVars[DurabilityVar.DefaultName];

    // 关键字：写进 CanonicalKeywords 后，悬停这张牌会自动带上「耐久」的说明（文本在 card_keywords 表）。
    // 子类要加自己的关键字，用 `base.CanonicalKeywords.Append(...)` 拼上，别整个覆写掉。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [Lihuowang2DurabilityKeyword.Value];

    // 变量：耐久变量 + 子类的 ExtraVars。
    // ⚠ 子类请覆写 ExtraVars，不要覆写 CanonicalVars，否则耐久变量会丢。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DurabilityVar(MaxDurability), .. ExtraVars];

    // 子类自己的数值变量（Damage / Block / 自定义 DynamicVar 等）
    protected virtual IEnumerable<DynamicVar> ExtraVars => [];

    // 进入战斗时把耐久补满：耐久按「每场战斗」计（和「消耗」一样，本场用尽、下场恢复）。
    // 若以后要做成"永久次数、用尽即从牌组移除"，改这里 + 下面的消耗处理即可。
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
            Durability.Refill();

        return base.AfterCardEnteredCombat(card);
    }

    // 每次打出后：耐久 -1；归零则把这张牌消耗掉（否则什么都不做，它会照常进弃牌堆）
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != this)
        {
            await base.AfterCardPlayed(choiceContext, cardPlay);
            return;
        }

        Durability.Consume();

        if (Durability.IsEmpty)
        {
            // 进消耗堆（与「黑太岁吃掉」「火袄真经吃掉状态牌」同一套 CardCmd.Exhaust 写法）
            await CardCmd.Exhaust(choiceContext, this);
        }

        await base.AfterCardPlayed(choiceContext, cardPlay);
    }
}
