using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 丹炉：每个自己回合开始时随机发一颗丹药进手牌。
// 开炉次数越多越容易"炸炉"（炸炉的效果由炸炉卡自己的 OnTurnEndInHand 结算：炸毁本 power）。
[RegisterPower]
public class DanLuPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 单层、不叠加
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/danlu32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/danlu84.png");

    // 开炉次数（第 4 炉起有几率炸出炸炉诅咒）。
    // 故意不加 [SavedProperty]：引擎的 [SavedProperty] 只有**遗物**支持
    // （引擎里 49 处用法全是遗物，SerializableRelic.Props 才会带上它）；
    // 战斗内的 Power 在存档和多人战斗快照（NetFullCombatState）里都只有 {id, amount} 会同步，
    // 所以加在这里是死代码。官方自己的计数型 Power（ConfusedPower / SandpitPower /
    // SlothPower / TenderPower）同样用普通字段。
    private int _turns = 0;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        Flash();
        _turns++;
        await GiveReward(choiceContext, player);

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 抽奖：0-250 大力丹；251-500 润血丹；之后按剩余区间给辟谷丹，开炉越久越可能给炸炉
    private async Task GiveReward(PlayerChoiceContext choiceContext, Player player)
    {
        // 多人同步：开炉随机必须走引擎的确定性随机流（CombatCardGeneration = 战斗内生成卡牌）
        int n = player.RunState.Rng.CombatCardGeneration.NextInt(0, 1000);
        if (n <= 10)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2YangShouDan>(Owner, PileType.Hand, 1, player);
            return;
        }
        if (n <= 250)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2DaLiDan>(Owner, PileType.Hand, 1, player);
            return;
        }

        if (n <= 500)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2RunXueDan>(Owner, PileType.Hand, 1, player);
            return;
        }

        // 第 4 炉前：辟谷丹；之后阈值随次数下降，越晚越容易炸炉
        if (_turns < 4)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2BiGuDan>(Owner, PileType.Hand, 1, player);
            return;
        }

        int threshold = 800 - _turns * 10;
        if (n <= threshold)
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2BiGuDan>(Owner, PileType.Hand, 1, player);
        }
        else
        {
            await CardPileCmd.AddToCombatAndPreview<lihuowang2CurseZhalu>(Owner, PileType.Hand, 1, player);
        }
    }
}
