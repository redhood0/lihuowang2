using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 迷惘：一张诅咒。
//   · 平时不能打出（IsPlayable 为 false）；
//   · 持有「迷惘之力」（MiWangZhiLiPower）时**可以主动打出**：随机击晕一个敌人、获得 1 点能量、抽 1 张牌；
//   · 回合结束时若此牌仍在手牌中：获得 1 层「混乱」（官方 ConfusedPower）；
//   · 关键字是「消耗」（Exhaust）：**只有被打出时**才进消耗堆，回合结束不会自动消失，
//     所以它会一直留在手里、每回合结束都触发一次混乱（改前是 Ethereal/虚无，回合结束就自动消耗掉）。
//
// ⚠ 为什么不能继续用 Unplayable 关键字：引擎的 Keywords 是**实例级缓存集合**
//   （CardModel.Keywords：首次访问时把 CanonicalKeywords 并进 _keywords 就缓存住了，_sts2_all.cs:74469），
//   关键字没法随状态变化 —— 留着它就会永远拦住这张牌，哪怕身上有迷惘之力。
//   官方对"条件性可打出"用的就是动态 IsPlayable（Clash：手牌全是攻击牌才能打出），
//   模组「替身人皮」也是同一套写法。所以这里改成动态 IsPlayable，代价是卡面不再有
//   「不能被打出」那个关键字标签（不可打出的红调 / 点击提示仍由引擎按 IsPlayable 给）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2CurseMiWang : ModCardTemplate
{
    // 费用 -1：与官方诅咒（疑虑 / Doubt 等，见 _sts2_all.cs:139967 的 base(-1, ...)）保持一致，
    // 卡面显示的是诅咒那种"无费用"的观感。
    private const int energyCost = -1;
    private const CardType type = CardType.Curse;
    private const CardRarity rarity = CardRarity.Curse;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    // 打出时获得的能量 / 抽的牌数
    private const decimal EnergyGain = 1m;
    private const int DrawCount = 1;

    // 卡图：images/cards/MiWang.png —— 这个文件名和类名不一致（其它卡都是 <类名>.png），
    // 所以这里写死路径，而不是 $"{GetType().Name}.png"。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/MiWang.png");

    // 悬停提示：
    //   · 「混乱」—— 描述里那个状态到底做什么，直接借用官方能力自己的说明面板；
    //   · 「迷惘之力」—— 说明这张牌为什么有时能打出（也就解释了打出效果是谁给的）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ConfusedPower>(),
        // HoverTipFactory.FromPower<MiWangZhiLiPower>()
    ];

    public lihuowang2CurseMiWang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 衍生牌标记：不能被"战斗内随机生成/发现"类效果抽到（与炸炉一致）。
    // 引擎注释原文：Used primarily for filtering cards out of random in-combat generation effects.
    public override bool CanBeGeneratedInCombat => false;

    // 衍生牌标记：不进修正器牌池（官方注释点名 AscendersBane / 远古诅咒就是靠这个从 Cursed Run 里排除的）。
    public override bool CanBeGeneratedByModifiers => false;

    // 诅咒不可升级：与官方诅咒一致（Doubt / Writhe / AscendersBane 都是 MaxUpgradeLevel => 0）。
    public override int MaxUpgradeLevel => 0;

    // 能不能打出：平时不能，持有「迷惘之力」时可以（见类注释里为什么不用 Unplayable 关键字）。
    // 与「替身人皮」同一套写法：没有 Owner 的场合（图鉴 / 预览）按"不可打出"处理。
    protected override bool IsPlayable
    {
        get
        {
            if (Owner?.Creature == null)
            {
                return false;   // 图鉴 / 预览：没有实际持有者，按诅咒处理
            }

            return Owner.Creature.HasPower<MiWangZhiLiPower>();
        }
    }

    // 视觉卡池（引擎：CardModel.FrameMaterial => VisualCardPool.FrameMaterial）。
    // 数据上这张牌仍属于 lihuowang2CardPool，但外观借用引擎的诅咒池：
    // 卡框用 card_frame_curse 的黑框，能量图标用无色图标（原版诅咒就是这样）。
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<CurseCardPool>();

    // 告诉引擎：回合结束时若此牌仍在手牌中，调用 OnTurnEndInHand。
    public override bool HasTurnEndInHandEffect => true;

    // 关键字：
    //   · Exhaust（消耗）—— 打出时进消耗堆（"主动打出换收益"是一次性的）。
    //     ⚠ 引擎在"回合结束手牌"这条路径只认 Ethereal（虚无），写 Exhaust **不会**在回合结束消耗，
    //     所以这张牌会一直留在手里、每回合结束都给 1 层「混乱」——这是本牌现在的设计。
    //     想让它"回合结束就自动消失"，把这里换回 CardKeyword.Ethereal 即可。
    //   ⚠ 这里**不能**加 Unplayable（见类注释）：那个关键字会让它永远打不出去，
    //     而本牌在持有迷惘之力时要能被主动打出。同结构的「炸炉」是永久不可打出，所以它仍然带那个关键字。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    // 打出（只有持有「迷惘之力」时才可能走到这里）：随机击晕一个敌人 + 获得 1 点能量 + 抽 1 张牌。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
        {
            return;
        }

        // 随机目标必须走引擎的确定性随机流（CombatTargets = 战斗内随机选目标），
        // 否则联机两边击晕的不是同一个敌人，会直接触发状态校验分歧。
        //（与模组遗物「朽木如意」击晕那一段完全同一套写法。）
        Creature? target = player.RunState.Rng.CombatTargets
            .NextItem(Owner.Creature.CombatState!.HittableEnemies);

        if (target != null)
        {
            await CreatureCmd.Stun(target);
        }

        await PlayerCmd.GainEnergy(EnergyGain, player);

        // 再抽 1 张牌：能量不亏+有牌进来，打出「迷惘」才算净赚一手。
        // 走引擎标准抽牌流程（抽牌堆空了自己洗、遵守手牌上限、会触发 Hook.ShouldDraw /
        // AfterCardDrawn 这些钩子，例如「禁止抽牌」的减益同样会挡住它）。
        // 注意：Draw 不会套用"抽牌数 +N"这类修正，数量就是传进去的 DrawCount。
        await CardPileCmd.Draw(choiceContext, DrawCount, player);
    }

    // 回合结束若此牌在手牌中：获得 1 层「混乱」。
    // 「混乱」是官方能力 ConfusedPower（骰子18 掷到 6 时上的也是它），文案与图标都由官方提供。
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<ConfusedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
    }
}
