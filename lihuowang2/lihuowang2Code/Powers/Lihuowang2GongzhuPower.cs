using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 李岁·公主形态：每个自己回合开始获得 1 人工制品 + 1 铜钱，并把一张 0 费黑太岁加入手牌。
[RegisterPower]
public class Lihuowang2GongzhuPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/gongzhu32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/gongzhu84.png");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        Flash();

        // 1. 人工制品 +1
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner, 1m, Owner, null);

        // 2. 铜钱 +1
        await MoneyPower.ApplyMoney(choiceContext, Owner, Owner, null);

        // 3. 0 费黑太岁进手牌
        ICombatState? cs = player.Creature.CombatState;
        if (cs != null)
        {
            CardModel? blackTaiSui = cs.CreateCard<Lihuowang2Heitaisui>(player);
            if (blackTaiSui != null)
            {
                blackTaiSui.SetToFreeThisTurn();
                await CardPileCmd.AddGeneratedCardToCombat(blackTaiSui, PileType.Hand, player);
            }
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }
}
