using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 替身人皮：需要携带心素、且生命大于最大生命一半才能打出。
// 自伤一半最大生命，把【这张卡从卡组里暂时拿走】并换得一件【人皮】遗物。
//
// 闭环：被打出的这张牌会先把卡组里的原件（DeckVersion）收进遗物保管，遗物生效（挡下致命伤害）后
// 再把那张牌还回卡组 —— 所以它只是"暂时"离开卡组，人皮烧掉后还能再用一次。详见 lihuowang2Relic_Renpi。
//
// 本场战斗内也是"暂时移除"：打出后既不进弃牌堆、也不进消耗堆，而是直接从战斗里消失
// （也没有 [消耗] 关键字了）—— 见下面的 GetResultLocationForCardPlay。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2XinSuSkin : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬停提示：描述里出现的「替身人皮」（濒死时挡下一次致命伤害并回复一半最大生命，
    // 生效后本牌回到卡组）。FromRelic 返回的是一组提示，所以用展开语法并进列表。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [.. HoverTipFactory.FromRelic<lihuowang2Relic_Renpi>()];

    public lihuowang2XinSuSkin() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 门禁：需要心素（或它的先祖版本「一炁·心素」，见 lihuowang2Relic_Xinsu.HasXinsu）+ 生命 > 一半最大生命
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature?.Player == null)
                return true; // 图鉴/预览场景不做限制
            Player player = Owner.Creature.Player;
            if (!lihuowang2Relic_Xinsu.HasXinsu(player))
                return false;
            return Owner.Creature.CurrentHp > Owner.Creature.MaxHp / 2;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 把这张牌从**卡组**里暂时拿走，并把它的完整数据（升级等级、附魔等）交给将要获得的人皮保管。
        //    战斗里打出的这张是卡组原件的"战斗副本"，副本通过 DeckVersion 指回卡组原件 ——
        //    要移除的是**原件**（副本在战斗结束后本来就会消失）。
        //    顺带记一下：移除前必须先 ToSerializable()，移除后卡就是 HasBeenRemovedFromState 状态了。
        SerializableCard? stored = null;
        CardModel? deckCard = DeckVersion;
        if (deckCard != null && deckCard.Pile?.Type == PileType.Deck)
        {
            stored = deckCard.ToSerializable();
            await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        }

        // 2. 获得一件**新的**人皮（不再给已破损的那件充能）：
        //    先做出可变实例、把保管的卡塞进去，再 Obtain —— 这样每张牌都对应自己的一件人皮，
        //    人皮生效时各自把自己的那张牌还回卡组。
        //（RelicModel.ToMutable() 的返回类型是 RelicModel，需要转回具体类型 —— 引擎自己的
        // RelicCmd.Obtain<T> 也是这么 (T) 转的。）
        lihuowang2Relic_Renpi relic = (lihuowang2Relic_Renpi)ModelDb.Relic<lihuowang2Relic_Renpi>().ToMutable();
        if (stored != null)
            relic.StoreCard(stored);
        await RelicCmd.Obtain(relic, player);

        // 3. 自伤：失去一半最大生命的生命
        decimal loss = Owner.Creature.MaxHp / 2m;
        await CreatureCmd.Damage(choiceContext, Owner.Creature, loss, 
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);
    }

    // 打出后**直接从本场战斗消失**：不进弃牌堆、也不进消耗堆。
    // 这是引擎自己的标准做法（基类对 Power 牌 / 分身牌就是这么返回的）：
    // PileType.None → 结算打出时走 MoveCardToResultPileAfterPlay 的 None 分支 →
    // CardPileCmd.RemoveFromCombat(this)（连带烟散特效一起播），卡就彻底离开本场战斗。
    // 注意：这比"加 [消耗] 关键字"更彻底 —— 消耗只是把它丢进消耗堆（消耗堆可以被查看、也会被消耗系效果统计到）。
    protected override CardLocation GetResultLocationForCardPlay()
        => new(Owner, PileType.None, CardPilePosition.Bottom);

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
