using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Cards;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 心灼剑（心蟠脊骨剑）：每场战斗开始时，将一张「破碎虚空斩」加入手牌。
// 这里给出的那一张额外带「虚无」——回合结束时若还留在手牌里就会被消耗掉（见 BeforeCombatStart）。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_XinZhuoJian : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 悬停提示：遗物描述里出现的「破碎虚空斩」，直接把这张牌的预览挂在遗物上。
    // 用带卡牌自身提示的版本（FromCardWithCardHoverTips），这样以后这张牌加了关键词也会自动带出来。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<lihuowang2BreakSpace>();

    // 战斗开始时把「破碎虚空斩」加入手牌，并给这一张追加「虚无」。
    //
    // 「虚无」只加在**遗物给出的这一张**上：AddKeyword 改的是这张卡的实例
    // （CardModel.AddKeyword → 实例私有的 LocalKeywords，会随实例一起深拷贝），
    // 所以牌库/卡牌奖励里拿到的同名卡不受影响。
    // （若哪天要改成"这张牌本身"就是虚无，把它写进 lihuowang2BreakSpace.CanonicalKeywords 即可。）
    //
    // 为什么不用现成的 CardPileCmd.AddToCombatAndPreview<T>()：
    //   它内部是 combatState.CreateCard<T>() → AddGeneratedCardToCombat()，但**不返回**新建的卡，
    //   拿不到实例就没法追加关键字。所以这里手动做同样的两步（同一套 API、同样的顺序，
    //   AddToCombatAndPreview 对"加入手牌"也只是多等 0.1 秒，没有额外表现）。
    public override async Task BeforeCombatStart()
    {
        ICombatState? combatState = Owner.Creature.CombatState;
        CardModel? card = combatState?.CreateCard<lihuowang2BreakSpace>(Owner);
        if (card != null)
        {
            // 虚无：回合结束时若此牌仍在手牌中，引擎会把它消耗掉（与 mod 里「炸炉」的写法一致）。
            card.AddKeyword(CardKeyword.Ethereal);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }

        await base.BeforeCombatStart();
    }
}
