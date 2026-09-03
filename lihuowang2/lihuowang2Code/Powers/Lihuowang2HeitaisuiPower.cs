using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

[RegisterPower]
public class Lihuowang2HeitaisuiPower : ModPowerTemplate
{
    // 每层每回合消耗1张手牌并抽1张牌，消耗到诅咒时获得的格挡
    private const decimal ExhaustPerLayer = 1m;
    private const decimal DrawPerLayer = 1m;
    private const decimal BlockOnCurse = 3m;

    // 类型，Buff或Debuff
    public override PowerType Type => PowerType.Buff;
    // 叠加类型：Counter 表示可叠加。每打出 1 张黑太岁牌层数 +1，
    // 层数即 Amount，每层每回合多触发一次「抽1、消耗1、若诅咒得3格挡」。
    public override PowerStackType StackType => PowerStackType.Counter;

    // 自定义图标路径。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/Heitaisui32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/Heitaisui84.png"
    );

    // 玩家每回合开始时触发：按层数（Amount）重复执行，每层抽1张、选1张手牌消耗，
    // 若消耗的是诅咒则获得3点格挡
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 只对自己的回合生效
        if (player.Creature != Owner) return;

        for (int layer = 0; layer < Amount; layer++)
        {
            await TriggerOnce(choiceContext, player);
        }
    }

    // 单层触发流程：先抽1张，再选1张手牌消耗；若消耗的是诅咒则获得3点格挡
    private async Task TriggerOnce(PlayerChoiceContext choiceContext, Player player)
    {
        // 1. 先抽 1 张
        await CardPileCmd.Draw(choiceContext, DrawPerLayer, player);

        // 2. 让玩家从手牌选 1 张来消耗（此时手牌已包含刚抽到的那张）
        IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
            filter: null,
            source: this);

        CardModel? card = selected.FirstOrDefault();
        if (card == null) return;

        // 3. 先记录是否为诅咒，再消耗
        bool isCurse = card.Type == CardType.Curse;
        await CardCmd.Exhaust(choiceContext, card);

        // 4. 若消耗的是诅咒，则获得格挡
        if (isCurse)
            await CreatureCmd.GainBlock(Owner, BlockOnCurse, ValueProp.Move, null);
    }
}
