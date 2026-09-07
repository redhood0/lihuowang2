using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace lihuowang2.Cards;

// 引擎没有 Scry 关键字，这里用官方选牌屏模拟「预见/观星」：
// 展示抽牌堆顶部的 N 张牌，玩家可弃置其中任意张，其余保持原位。
public static class Lihuowang2ScryUtil
{
    public static async Task ScryAndDiscardAny(PlayerChoiceContext choiceContext, Player player, int count)
    {
        if (count <= 0)
            return;

        var top = PileType.Draw.GetPile(player).Cards.Take(count).ToList();
        if (top.Count == 0)
            return;

        var selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            top,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, top.Count)
            {
                Cancelable = true
            });

        foreach (var card in selected)
            await CardPileCmd.Add(card, PileType.Discard);
    }
}
