using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using lihuowang2.Characters;
using lihuowang2.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 迷惘天道·季灾：本角色的第二张「先祖」卡（CardRarity.Ancient），X 费。
// 效果（X = 打出时实际消耗掉的能量）：
//   1. 把 X 张「迷惘」诅咒**随机位置**洗进抽牌堆（带"卡牌飞入牌堆"的入堆预览动画）；
//   2. 抽 X 张牌；
//   3. 获得「迷惘之力」1 回合（升级后 2 回合）—— 有了它，那些迷惘才能被主动打出换收益
//      （击晕随机一个敌人 + 获得 1 点能量，见 lihuowang2CurseMiWang.OnPlay）；
//   4. 本牌是**能力牌**（CardType.Power）：打出后常驻场上，不回抽牌堆、也不进弃牌堆。
//
// X 费的写法（引擎自带的 Cascade / 旋风类卡就是这一套）：
//   · 覆写 HasEnergyCostX => true；
//   · 构造函数里的费用传 -1（引擎据此把卡面渲染成 X，并在打出时消耗掉全部能量）；
//   · 打出时用 ResolveEnergyXValue() 取回"这张牌实际消耗了多少能量"= X。
//
// 先祖卡的两处登记（与已有的「岁岁公主」完全一致）：
//   · [RegisterCard(typeof(lihuowang2CardPool))] —— 注册进本角色卡池。
//     先祖卡不会进随机奖励池（引擎在 CardFactory 里就按 Ancient / Event 稀有度排除了）；
//   · [RegisterDustyTomeCard(typeof(lihuowang2Character))] —— 登记为「尘封古籍」（达弗事件里的
//     先古遗物 DustyTome）的候选。不登记的话它不会出现在那件遗物的候选池里。
[RegisterCard(typeof(lihuowang2CardPool))]
[RegisterDustyTomeCard(typeof(lihuowang2Character))]
public sealed class lihuowang2JiZai : ModCardTemplate
{
    // 费用 -1 + HasEnergyCostX => 卡面显示 X 费
    private const int energyCost = -1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Ancient;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 迷惘之力的持续回合数（= 施加的层数；该能力每回合结束 -1 层）
    private const decimal PowerTurnsBase = 1m;
    private const decimal PowerTurnsUpgraded = 2m;

    // 卡图：images/cards/jizai.png —— 文件名和类名不一致（其它卡都是 <类名>.png），所以写死路径。
    // 先祖卡专用的边框 / 文本底 / 横幅交给引擎按稀有度处理，这里不覆盖。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/jizai.png");

    // 悬停提示：
    //   · 「迷惘之力」—— 描述里点名了它，说明那些迷惘为什么有时能打出；
    //   · 「迷惘」—— 直接把那张诅咒牌做成一张 hover 卡面（HoverTipFactory.FromCard），
    //     鼠标停在别的任意位置就能看清"洗进去的到底是什么牌"。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<MiWangZhiLiPower>(),
        HoverTipFactory.FromCard<lihuowang2CurseMiWang>()
    ];

    public lihuowang2JiZai() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // X 费：卡面显示 X，打出时消耗全部能量（引擎机制，不用自己写）。
    protected override bool HasEnergyCostX => true;

    // 升级：只影响迷惘之力的持续回合数（1 → 2），在 OnPlay 里按 IsUpgraded 取值，
    // 所以这里不需要 OnUpgrade。升级次数用引擎默认（1 次）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
        {
            return;
        }

        int x = ResolveEnergyXValue();

        if (x > 0)
        {
            // 1. 先把 X 张「迷惘」**全部**洗进抽牌堆（每张随机插一个位置）。
            //    这一步必须整体跑完再往下走 —— 否则抽牌时它们还没进堆，"有机会抽上来"就不成立。
            //    逐张走 CreateCard + AddGeneratedCardToCombat：战斗内生成牌的标准入堆流程
            //   （写战斗历史 + 飞牌入堆动画，与模组「我有病」把诅咒洗进抽牌堆同一套）。
            //    随机位置由引擎给：CardPilePosition.Random = Rng.Shuffle.NextInt(牌堆数 + 1)，
            //    见 _sts2_all.cs:197810。
            //    不走 AddToCombatAndPreview 是因为它把预览动画排在整批入堆之后、自带 1 秒等待；
            //    这里自己控制顺序：全部入堆 → 播入堆预览动画 → 再抽牌。
            List<CardPileAddResult> added = new(x);
            for (int i = 0; i < x; i++)
            {
                CardModel? curse = Owner.Creature.CombatState!.CreateCard<lihuowang2CurseMiWang>(player);
                if (curse == null)
                {
                    continue;
                }

                added.Add(await CardPileCmd.AddGeneratedCardToCombat(curse, PileType.Draw, player,
                    CardPilePosition.Random));
            }

            // 2. 洗入动画：这几张迷惘先亮出来、再飞向抽牌堆（引擎自带的"卡牌加入牌堆"预览，
            //    与模组「大千录」把大千录牌洗进抽牌堆时同一套）。
            await PreviewPileAdd(added);

            // 3. 洗完之后再抽 X 张：抽的时候那 X 张迷惘确实已经在抽牌堆里了（有机会抽上来）。
            await CardPileCmd.Draw(choiceContext, x, player);
        }

        // 4. 获得「迷惘之力」1 回合（升级 2 回合）。
        //    层数就是维持的回合数：该能力在持有者每个回合结束时 -1，归零时引擎自动移除。
        await PowerCmd.Apply<MiWangZhiLiPower>(choiceContext, Owner.Creature,
            IsUpgraded ? PowerTurnsUpgraded : PowerTurnsBase, Owner.Creature, this);
    }

    // 播放「卡牌加入牌堆」的预览动画（与引擎 CardPileCmd.AddToCombatAndPreview 内部一致：
    // 卡牌从屏幕中央弹出、停留，然后飞向对应牌堆；与模组「大千录」的 PreviewPileAdd 同一写法）。
    private static async Task PreviewPileAdd(IReadOnlyList<CardPileAddResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        CardPreviewStyle style = results.Count <= 5
            ? CardPreviewStyle.HorizontalLayout
            : CardPreviewStyle.MessyLayout;
        CardCmd.PreviewCardPileAdd(results, 1.2f, style);
        await Cmd.Wait(1f);
    }
}
