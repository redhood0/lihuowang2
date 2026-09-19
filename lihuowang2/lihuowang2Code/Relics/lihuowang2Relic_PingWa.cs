using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using lihuowang2.Cards;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 平娃：右键花 1 点能量「预见 4」；若处于丹阳子化则免费预见。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_PingWa : ModRelicTemplate, IModRightClickableRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 悬停时附上「预见」关键词说明（仅用于显示，不给遗物加玩法关键字）。
    // 关键词本体见 Keywords/Lihuowang2Keywords.cs，文本在 card_keywords 表的
    // LIHUOWANG2_KEYWORD_FORESEE.title / .description。
    protected override IEnumerable<string> RegisteredKeywordIds => ["LIHUOWANG2_KEYWORD_FORESEE"];

    private static bool HasDanyang(MegaCrit.Sts2.Core.Entities.Players.Player player)
        => player.Creature.HasPower<Lihuowang2DanyangziPower>();

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        var player = context.Player;
        return player != null && (HasDanyang(player) || player.PlayerCombatState?.Energy > 0);
    }

    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        return player != null && (HasDanyang(player) || player.PlayerCombatState?.Energy > 0);
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        if (player == null)
            return;

        // 丹阳子化下免费；否则花 1 点能量
        if (!HasDanyang(player))
            await PlayerCmd.LoseEnergy(1m, player);

        await Lihuowang2ScryUtil.ScryAndDiscardAny(context.PlayerChoiceContext, player, 4);
    }
}
