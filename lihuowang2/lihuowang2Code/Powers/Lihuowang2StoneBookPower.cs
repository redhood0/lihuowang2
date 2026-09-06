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

// 天书：每回合结束（玩家回合）获得 6 点格挡。
[RegisterPower]
public class Lihuowang2StoneBookPower : ModPowerTemplate
{
    private const decimal BlockPerTurnEnd = 6m;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/StoneBook32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/StoneBook84.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 只在玩家回合结束时生效
        if (side == CombatSide.Player)
            await CreatureCmd.GainBlock(Owner, BlockPerTurnEnd, ValueProp.Move, null);

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }
}
