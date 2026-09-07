using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 丹阳子化：透支未来——一次性获得大量力量与敏捷和缓冲，代价是每回合能量惩罚递增。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2EatMeat : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // StrDex = 获得的力量/敏捷（7，升级 +3）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("StrDex", 7m)
    ];

    public lihuowang2EatMeat() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // BGM：丹阳子附体
        Lihuowang2MusicUtil.PlayCardMusic("shifu.mp3");

        Creature self = Owner.Creature;
        decimal stat = DynamicVars["StrDex"].BaseValue;

        // 1. 力量、敏捷、缓冲
        await PowerCmd.Apply<StrengthPower>(choiceContext, self, stat, self, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, self, stat, self, this);
        await PowerCmd.Apply<BufferPower>(choiceContext, self, 1m, self, this);

        // 2. 代价：丹阳子化
        await PowerCmd.Apply<Lihuowang2DanyangziPower>(choiceContext, self, 1m, self, this);
    }

    // 升级：力量/敏捷 7 → 10
    protected override void OnUpgrade()
    {
        DynamicVars["StrDex"].UpgradeValueBy(3);
    }
}
