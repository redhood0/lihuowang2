using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Powers;

// 铜钱：撒币累积的标记层数，上限 5 层。铜钱系卡牌据此结算额外效果。
[RegisterPower]
public class MoneyPower : ModPowerTemplate
{
    // 类型：Buff
    public override PowerType Type => PowerType.Buff;
    // 叠加：Counter，层数 = 铜钱数（上限 5）
    public override PowerStackType StackType => PowerStackType.Counter;

    // 铜钱层数上限
    public const int MaxCopper = 5;

    // 图标先用现成 Heitaisui 占位，有正式图后替换路径
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/tongqian32.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/tongqian84.png");

    // 统一施加铜钱：已达上限则不再叠加
    public static async Task ApplyMoney(PlayerChoiceContext choiceContext, Creature target,
        Creature? applier, CardModel? cardSource)
    {
        MoneyPower? existing = target.GetPower<MoneyPower>();
        if (existing != null && existing.Amount >= MaxCopper)
            return;

        await PowerCmd.Apply<MoneyPower>(choiceContext, target, 1m, applier, cardSource);
    }
}
