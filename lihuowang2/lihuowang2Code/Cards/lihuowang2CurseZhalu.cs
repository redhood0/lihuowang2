using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 炸炉：丹炉炼多后可能炸出的一张诅咒。不能打出。
// 引擎在玩家回合结束、弃手牌之前会调用 OnTurnEndInHand（由 HasTurnEndInHandEffect 开启）：
// 炸毁丹炉、失去 1 点生命；随后因 Ethereal（虚无）关键字被消耗。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2CurseZhalu : ModCardTemplate
{
    private const int energyCost = -2;
    private const CardType type = CardType.Curse;
    private const CardRarity rarity = CardRarity.Curse;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public lihuowang2CurseZhalu() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 诅咒无法打出
    protected override bool IsPlayable => false;

    // 视觉卡池（引擎：CardModel.FrameMaterial => VisualCardPool.FrameMaterial）。
    // 数据上这张牌仍属于 lihuowang2CardPool，但外观借用引擎的诅咒池：
    // 卡框用 card_frame_curse 的黑框，能量图标用无色图标（原版诅咒就是这样）。
    // 否则会跟着本角色卡池的红色 PoolFrameMaterial 被染成红框。
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<CurseCardPool>();

    // 告诉引擎：回合结束时若此牌仍在手牌中，调用 OnTurnEndInHand。
    public override bool HasTurnEndInHandEffect => true;

    // 虚无：让引擎在 OnTurnEndInHand 跑完后的结算阶段把这张牌消耗掉。
    // 注意：引擎在"回合结束手牌"这条路径只认 Ethereal，写 Exhaust 关键字不会消耗。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    // 回合结束若此牌在手牌中：炸毁丹炉，并失去 1 点生命。
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        // 炸毁丹炉（把施加在自己身上的丹炉 power 移除）
        DanLuPower? danlu = Owner.Creature.GetPower<DanLuPower>();
        if (danlu != null)
        {
            await PowerCmd.Remove(danlu);
        }

        // 失去 1 点生命（无视格挡）
        await CreatureCmd.Damage(choiceContext, Owner.Creature, 1m,
            ValueProp.Unblockable, Owner.Creature, null, null);
    }
}
