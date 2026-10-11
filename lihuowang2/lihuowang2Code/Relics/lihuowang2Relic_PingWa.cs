using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using lihuowang2.Cards;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 平娃：右键花 1 点能量「预见 4」，每回合限一次；若处于丹阳子化则免费预见。
// 用过之后当回合图标变灰（RelicStatus.Disabled），下个回合开始时恢复。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_PingWa : ModRelicTemplate, IModRightClickableRelic
{
    // 本回合用过没有。不入档：RelicModel.Status 本身就不写进存档，
    // 而且这是回合内的临时状态 —— 读档回来按"还能用"处理更合理（不会读档后一直灰着）。
    private bool _usedThisTurn;

    public override RelicRarity Rarity => RelicRarity.Common;

    // 「已使用」：用过 → 变灰，新回合开始 → 恢复。
    // 灰是靠 RelicStatus.Disabled 实现的 —— 引擎 RelicModel.UpdateTexture 里 Disabled 就是把
    // 材质的 _isUsed 着色参数打开（官方遗物通用的那种"已使用"灰），纯视觉、不影响钩子派发，
    // 所以回合开始那个恢复钩子照样会执行。
    private bool UsedThisTurn
    {
        get => _usedThisTurn;
        set
        {
            if (_usedThisTurn == value)
            {
                return;
            }

            _usedThisTurn = value;
            Status = value ? RelicStatus.Disabled : RelicStatus.Normal;
        }
    }

    // 回合开始恢复可用。
    // 这个钩子会把"每个玩家的回合开始"都派发一遍（联机时队友的回合也会通知到，见模组 XingShanPower 的注释），
    // 所以必须只认自己的回合 —— 与官方 BoneTea 同样的守卫写法。
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        UsedThisTurn = false;
        return Task.CompletedTask;
    }

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 悬停时附上「预见」关键词说明（仅用于显示，不给遗物加玩法关键字）。
    // 关键词本体见 Keywords/Lihuowang2Keywords.cs，文本在 card_keywords 表的
    // LIHUOWANG2_KEYWORD_FORESEE.title / .description。
    protected override IEnumerable<string> RegisteredKeywordIds => ["LIHUOWANG2_KEYWORD_FORESEE"];

    private static bool HasDanyang(Player player)
        => player.Creature.HasPower<Lihuowang2DanyangziPower>();

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        var player = context.Player;
        return player != null && !_usedThisTurn
            && (HasDanyang(player) || player.PlayerCombatState?.Energy > 0);
    }

    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        return player != null && !_usedThisTurn
            && (HasDanyang(player) || player.PlayerCombatState?.Energy > 0);
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        if (player == null || _usedThisTurn)
            return;

        // 丹阳子化下免费；否则花 1 点能量
        if (!HasDanyang(player))
            await PlayerCmd.LoseEnergy(1m, player);

        // 用掉本回合的这一次：立刻变灰（下个回合开始由 AfterPlayerTurnStart 恢复）。
        UsedThisTurn = true;

        await Lihuowang2ScryUtil.ScryAndDiscardAny(context.PlayerChoiceContext, player, 4);
    }
}
