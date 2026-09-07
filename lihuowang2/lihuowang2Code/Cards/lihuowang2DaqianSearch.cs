using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
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

// 大千录·寻：从消耗堆里把大千录牌拿回手牌（可选，最多拿 magic 张）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2DaqianSearch : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // Amount = 最多回手的数量（2，升级 +1 → 3）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Amount", 2m)
    ];

    public lihuowang2DaqianSearch() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        int max = (int)DynamicVars["Amount"].BaseValue;
        CardPile exhaustPile = PileType.Exhaust.GetPile(player);
        List<CardModel> candidates = exhaustPile.Cards.Where(IsDaqianlu).ToList();
        if (candidates.Count == 0)
            return;

        // 张数足够少：全部拿回手牌
        IEnumerable<CardModel> toReturn;
        if (candidates.Count <= max)
        {
            toReturn = candidates;
        }
        else
        {
            // 否则弹出选牌界面，最多选 max 张，可取消
            toReturn = await CardSelectCmd.FromCombatPile(
                choiceContext, exhaustPile, player,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, max) { Cancelable = true },
                IsDaqianlu);
        }

        foreach (CardModel card in toReturn)
            await CardPileCmd.Add(card, PileType.Hand);
    }

    // 是否属于大千录系列（含本体与五行/有福同享等带 Tag 的衍生牌）
    private static bool IsDaqianlu(CardModel card) => card switch
    {
        lihuowang2Daqianlu or lihuowang2DaqianSearch or
        lihuowang2DaqianArm or lihuowang2DaqianEye or lihuowang2DaqianFinger or
        lihuowang2DaqianFireSkin or lihuowang2DaqianNail or lihuowang2DaqianRibs or
        lihuowang2DaqianSkin or lihuowang2DaqianTeeth or lihuowang2DaqianWuxing or
        lihuowang2DaqianYoufutongxiang => true,
        _ => false
    };

    // 升级：费用 1 → 0，回手上限 +1
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["Amount"].UpgradeValueBy(1);
    }
}
