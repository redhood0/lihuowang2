using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Potions;
using lihuowang2.Minions;

namespace lihuowang2.Patches;

// 让官方「污浊药水」(FoulPotion) 的伤害也落到**修真随从**（和尚 / 彭龙腾 / 秋吃饱）身上。
//
// 原因：官方 OnUse 打的是 `CombatState.Creatures.Where(c => !c.IsPet)`，
// 而 MinionLib 的随从是通过 `PlayerCmd.AddPet<T>` 召唤的（PetOwner != null → IsPet == true），
// 所以被当成宠物排除掉了 —— 表现为"只有玩家和怪物掉血，随从毫发无损"。
//
// 做法：只在**战斗内**接管这一句目标选择，把修真随从额外加进目标；
// 非战斗场景（把药水扔给商人 / 假商人）返回 true，完全走官方原逻辑。
internal static class FoulPotionHitsXiuZhenMinionsPatch
{
    private const string HarmonyId = "lihuowang2.foul_potion_hits_xiuzhen_minions";

    private static bool _applied;

    public static void Apply(Logger logger)
    {
        if (_applied)
        {
            return;
        }

        try
        {
            var harmony = new Harmony(HarmonyId);

            MethodInfo? target = AccessTools.Method(
                typeof(FoulPotion),
                "OnUse",
                [typeof(PlayerChoiceContext), typeof(Creature)]);

            MethodInfo? prefix = AccessTools.Method(
                typeof(FoulPotionHitsXiuZhenMinionsPatch),
                nameof(Prefix));

            if (target == null || prefix == null)
            {
                logger.Warn("FoulPotion patch target not found; potion will not damage xiu-zhen minions.");
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            _applied = true;
            logger.Info("FoulPotion patch applied: xiu-zhen minions now take Foul Potion damage.");
        }
        catch (Exception ex)
        {
            logger.Warn($"FoulPotion patch failed: {ex.Message}");
        }
    }

    // 只在战斗内接管；商店 / 假商人的分支交回官方实现。
    private static bool Prefix(FoulPotion __instance, PlayerChoiceContext choiceContext, ref Task __result)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return true;
        }

        Creature owner = __instance.Owner.Creature;
        DamageVar damage = __instance.DynamicVars.Damage;

        // 与官方一致地排除宠物，但把**修真随从**放回来。
        List<Creature> targets = owner.CombatState.Creatures
            .Where(c => !c.IsPet || IsXiuZhenMinion(c))
            .ToList();

        __result = DamageTargets(choiceContext, owner, targets, damage);
        return false;
    }

    private static async Task DamageTargets(
        PlayerChoiceContext choiceContext,
        Creature owner,
        List<Creature> targets,
        DamageVar damage)
    {
        await CreatureCmd.Damage(choiceContext, targets, damage.BaseValue, damage.Props, owner, null, null);
    }

    // 「修真」三只随从：和尚 / 彭龙腾 / 秋吃饱。
    private static bool IsXiuZhenMinion(Creature creature)
    {
        return creature.Monster is HeShang or PengLongTeng or QiuChiBao;
    }
}
