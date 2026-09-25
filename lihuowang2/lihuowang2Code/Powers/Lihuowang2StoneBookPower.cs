using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 天书：每个玩家回合结束时获得 6 × 层数 点格挡。
// 层数就是能力图标上的数字（每打出/再获得一次「天书」+1 层）。
[RegisterPower]
public class Lihuowang2StoneBookPower : ModPowerTemplate
{
    // 每层提供的格挡
    private const decimal BlockPerStack = 6m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/StoneBook32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/StoneBook84.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 只在玩家回合结束时生效：格挡 = 层数 × 6
        if (side == CombatSide.Player)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, base.Amount * BlockPerStack, ValueProp.Unpowered, null);
        }

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }
}
