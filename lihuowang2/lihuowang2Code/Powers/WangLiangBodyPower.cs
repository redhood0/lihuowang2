using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 魍魉之躯：每抽到 2 张诅咒牌，获得 amount 点力量。
[RegisterPower]
public class WangLiangBodyPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加：Counter，层数 = 每 2 张诅咒给予的力量
    public override PowerStackType StackType => PowerStackType.Counter;

    // 已抽到的诅咒计数
    private int _curseDrawn;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/wangliang32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/wangliang84.png");

    // 抽到诅咒时累计，每满 2 张获得力量并清零
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Type != CardType.Curse)
            return;

        _curseDrawn++;
        if (_curseDrawn < 2)
            return;

        _curseDrawn = 0;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, base.Amount, Owner, null);
    }
}
