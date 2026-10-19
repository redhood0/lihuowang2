using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace lihuowang2.Cards;

// 「每当预见」的回调接口：能力/遗物实现它，就会在每次预见**真正发生**后收到通知。
// 例：能力牌「虚拟？现实」（Powers/VirtualRealityPower）。
public interface IForeseeListener
{
    Task OnForesee(PlayerChoiceContext choiceContext, Player player);
}

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

        // 预见已真正发生 → 通知所有「每当预见」的监听者（能力/遗物）。
        // 先 ToList()：监听者内部可能施加能力/造成伤害，避免边遍历边改集合。
        foreach (IForeseeListener listener in player.Creature.Powers.OfType<IForeseeListener>().ToList())
        {
            await listener.OnForesee(choiceContext, player);
        }
    }
}
