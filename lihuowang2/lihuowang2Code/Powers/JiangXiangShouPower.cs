using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 将相首（随从「彭龙腾」身上的能力）：
// 召唤者的回合开始时，随机获得 1 张「战士」的卡牌，该牌本回合耗能为 0。
//
// 「战士」= 官方「铁甲战士」Ironclad 的卡池（不是模组自己的 lihuowang2CardPool）。
// 只取「已解锁且符合单人/多人限制」的牌，与模组「以假修真」取无色牌的方式一致。
[RegisterPower]
public class JiangXiangShouPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // 不吃层数：每只彭龙腾每回合都只发 1 张（数量不随层数变化），图标上也不显示数字。
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标：images/powers/jiangxiangshou.png（同一张图兼作小/大图标）
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/jiangxiangshou.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/jiangxiangshou.png");

    // 回合开始：随机 1 张战士牌 → 本回合 0 费 → 进手牌。
    // 与模组「李岁·公主」「丹炉」同一套写法：CardFactory.GetDistinctForCombat + 引擎的确定性随机流。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 随从自己没有回合：Hook.PlayerTurnStart 会把「每个玩家的回合开始」都通知一遍，
        // 所以必须自己筛出召唤者本人的那一次，否则队友的回合也会替我们发牌。
        if (Owner.PetOwner != player)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        List<CardModel> candidates = ModelDb.CardPool<IroncladCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();
        if (candidates.Count == 0)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        // 多人同步：随机必须走引擎的确定性随机流（CombatCardGeneration = 战斗内生成卡牌），
        // 否则两边玩家抽到的牌不同，会直接触发联机的状态校验分歧。
        CardModel? card = CardFactory
            .GetDistinctForCombat(player, candidates, 1, player.RunState.Rng.CombatCardGeneration)
            .FirstOrDefault();
        if (card != null)
        {
            Flash();

            // 「费用变为 0」用引擎的"本回合免费"：SetToFreeThisTurn = 能量费与星费都置 0，打出或回合结束即恢复。
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }
}
