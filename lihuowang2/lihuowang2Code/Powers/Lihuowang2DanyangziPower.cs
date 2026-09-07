using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 丹阳子化：获得巨额力量/敏捷的代价。每回合开始时失去能量，且每回合多失去 1 点（上限封顶后持平）。
[RegisterPower]
public class Lihuowang2DanyangziPower : ModPowerTemplate
{
    // 能量惩罚上限（源端按能量上限封顶；这里给一个安全上限）
    private const int MaxPenalty = 10;

    // 当前回合的能量惩罚
    private int _penalty = 1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/danyangzi32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/danyangzi84.png");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        Flash();
        await PlayerCmd.LoseEnergy(_penalty, player);
        if (_penalty < MaxPenalty)
            _penalty++;

        await base.AfterPlayerTurnStart(choiceContext, player);
    }
}
