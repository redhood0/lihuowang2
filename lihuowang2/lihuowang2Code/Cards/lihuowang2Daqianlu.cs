using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Daqianlu : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2Daqianlu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 将 4 张随机大千录牌加入抽牌堆（权重与原版一致）
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        ICombatState? combatState = Owner.Creature.CombatState;
        if (player == null || combatState == null)
            return;

        for (int i = 0; i < 4; i++)
        {
            CardModel? card = CreateRandomDaqian(combatState, player, Random.Shared.Next(16));
            if (card != null)
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player);
        }

        // 绝境彩蛋：仅剩 1 点生命且能量打空时，把 0 费「登阶」塞回手牌
        if (Owner.Creature.CurrentHp <= 1 && Owner.PlayerCombatState!.Energy <= 0)
        {
            CardModel? dengjie = combatState.CreateCard<lihuowang2DaqianDengjie>(player);
            if (dengjie != null)
            {
                dengjie.SetToFreeThisTurn();
                await CardPileCmd.AddGeneratedCardToCombat(dengjie, PileType.Hand, player);
            }
        }
    }

    private static CardModel? CreateRandomDaqian(ICombatState combatState, Player player, int roll)
    {
        // 原版大千录列表权重：剜眼×2 肋间×1 皮×4 指甲×2 指×2 牙×2 手臂×2 火皮×1 共16格
        return roll switch
        {
            < 2 => combatState.CreateCard<lihuowang2DaqianEye>(player),
            < 3 => combatState.CreateCard<lihuowang2DaqianRibs>(player),
            < 7 => combatState.CreateCard<lihuowang2DaqianSkin>(player),
            < 9 => combatState.CreateCard<lihuowang2DaqianNail>(player),
            < 11 => combatState.CreateCard<lihuowang2DaqianFinger>(player),
            < 13 => combatState.CreateCard<lihuowang2DaqianTeeth>(player),
            < 15 => combatState.CreateCard<lihuowang2DaqianArm>(player),
            _ => combatState.CreateCard<lihuowang2DaqianFireSkin>(player)
        };
    }

    // 升级：费用 1 → 0
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
