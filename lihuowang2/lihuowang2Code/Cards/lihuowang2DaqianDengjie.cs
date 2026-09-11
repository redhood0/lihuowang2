using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using lihuowang2.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 登阶：大千录绝境翻盘卡。
// 只有在 1 血左右被大千录(隐藏触发)以 0 费塞进手牌时，才真正价值连城。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianDengjie : ModCardTemplate
{
    private const int energyCost = 5;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 打出后消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Draw = 抽牌数(4，升级 +2)；Threshold = 血量低于该值触发绝境(8，升级 +2)
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Draw", 4m),
        new DynamicVar("Threshold", 8m)
    ];

    // 悬停提示：描述里出现的易伤 / 无实体 / 再生，以及登阶遗物。
    // （[gold]消耗[/gold] 由 CanonicalKeywords 里的 Exhaust 自动补。）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<IntangiblePower>(),
        HoverTipFactory.FromPower<RegenPower>(),
        .. HoverTipFactory.FromRelic<lihuowang2Relic_Dengjie>()
    ];

    public lihuowang2DaqianDengjie() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 抽牌
        await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].BaseValue, player);

        // 2. 所有敌人 99 层易伤
        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState!.Enemies;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemies, 99m, Owner.Creature, this);

        // 3. 血量低于门槛：绝境爆发（只有满足条件才播 BGM）
        if (Owner.Creature.CurrentHp > (int)DynamicVars["Threshold"].BaseValue)
            return;

        // BGM：苍蜣登阶（音量走 Lihuowang2MusicUtil.DefaultCardMusicVolume，默认 50%）
        Lihuowang2MusicUtil.PlayCardMusic("dengjie.mp3");

        await PlayerCmd.GainEnergy(2m, player);
        await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 没有登阶遗物就先把它拿到手（遗物自带计数器，初始即登阶 1），已有则登阶 +1。
        lihuowang2Relic_Dengjie? dengjieRelic = player.GetRelic<lihuowang2Relic_Dengjie>();
        if (dengjieRelic == null)
        {
            dengjieRelic = await RelicCmd.Obtain<lihuowang2Relic_Dengjie>(player);
        }
        else
        {
            dengjieRelic.StepUp();
        }

        dengjieRelic?.Flash();

        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, 8m, Owner.Creature, this);
    }

    // 升级：费用 5 → 4，抽牌 +2，门槛 +2
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["Draw"].UpgradeValueBy(2);
        DynamicVars["Threshold"].UpgradeValueBy(2);
    }
}
