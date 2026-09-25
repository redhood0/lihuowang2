using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 骰子18：右键花 1 点能量抽 1~6 张牌；掷到 6 时获得混乱并破损。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_Dice18 : ModRelicTemplate, IModRightClickableRelic
{
    private bool _usedUp;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool IsUsedUp => _usedUp;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 遗物文案：平时显示 .normalDescription，破损（UsedUp）后换成 .brokenDescription 的彩蛋。
    // 引擎渲染遗物文本读的是 DynamicVar 的 ToString()（官方的 StringVar / CalculatedVar 都是这么供值的），
    // 所以在变量里按状态返回不同文本就行，不用打补丁；relics 表里的 .description 写成 {RelicText} 当占位。
    private sealed class RelicTextVar : StringVar
    {
        public RelicTextVar() : base("RelicText") { }

        public override string ToString()
        {
            string entry = _owner?.Id.Entry ?? string.Empty;
            bool usedUp = _owner is lihuowang2Relic_Dice18 { IsUsedUp: true };
            LocString? text = LocString.GetIfExists("relics",
                entry + (usedUp ? ".brokenDescription" : ".normalDescription"));
            return text?.GetRawText() ?? string.Empty;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [new RelicTextVar()];

    public bool CanHandleRightClickLocal(ModRightClickContext context)
        => context.Player.Creature != null && !_usedUp;

    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
        => !_usedUp;

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        var player = context.Player;
        if (player == null || _usedUp)
            return;

        // 消耗 1 点能量
        await PlayerCmd.LoseEnergy(1m, player);

        // 掷骰 1~6
        int roll = Random.Shared.Next(1, 7);

        // 掷到 6：获得混乱并破损
        if (roll == 6)
        {
            await PowerCmd.Apply<ConfusedPower>(
                context.PlayerChoiceContext, player.Creature, 1m, player.Creature, null);
            _usedUp = true;
            Status = RelicStatus.Disabled;
        }

        // 抽 roll 张牌
        await CardPileCmd.Draw(context.PlayerChoiceContext, roll, player);
    }
}
