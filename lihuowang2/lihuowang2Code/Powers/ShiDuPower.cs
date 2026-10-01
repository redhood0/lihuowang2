using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Potions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 石渎（随从「秋吃饱」身上的能力）：召唤者的回合结束时，获得「药水形状的石头」。
//
// 「药水形状的石头」是引擎自带的道具 PotionShapedRock（PotionRarity.Token、
// 战斗中使用、对敌人造成 15 点无力量加成的伤害），不用自己造内容。
[RegisterPower]
public class ShiDuPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;

    // 不吃层数：无论有多少只秋吃饱（每只一份 power），每只每回合都只给 1 个石头。
    // 用 Single 而不是 Counter，图标上也就不会显示层数数字。
    public override PowerStackType StackType => PowerStackType.Single;

    // 图标直接用 shidu.png（一张图同时充当小/大图标）
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/shidu.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/shidu.png");

    // 悬停提示：把「药水形状的石头」的属性面板挂出来，玩家能直接看清这是个什么东西。
    // 注意 ModPowerTemplate 把 ExtraHoverTips 封了，扩展点叫 AdditionalHoverTips。
    // protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    //     [HoverTipFactory.FromPotion<PotionShapedRock>()];

    // 召唤者的回合结束时发放。
    // 用 AfterSideTurnEnd + side/participants 双重判断（和模组「天书」同一套写法）：
    // 多人时每个玩家各有一个回合结束，只认召唤者本人的那一次，不会替队友发。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        Player? summoner = Owner.PetOwner;
        if (side != CombatSide.Player || summoner == null ||
            !participants.Contains(summoner.Creature))
            return;

        Flash();

        // 固定给 1 个（不吃层数）。
        // 药水槽满时 TryToProcure 会以 TooFull 失败：石头不会掉在地上，静默跳过即可。
        await PotionCmd.TryToProcure<PotionShapedRock>(summoner);

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }
}
