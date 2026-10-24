using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 青椒肉丝：0 费技能，消耗。
//   失去 3 点生命 → 使**另一个**友方单位回复 3 点生命，并移除 1 层随机负面效果。
//   升级：回复量 +2（3 → 5），并**去掉「消耗」**（打完进弃牌堆，之后还能再抽到、再用）。
//
// 目标类型是**自定义**的（见下面 AllyCreatureTarget）：原版 TargetType 里只有
// Self / AnyPlayer / AnyAlly（= 其他玩家）/ Osty，没有"其他任意友方生物"这一项，
// 所以用 RitsuLib 的 CustomTargetType 注册一个 —— 玩家可以把治疗给**随从**或队友，
// 但**不能给自己**。卡面文案只写"友方单位"，不点破随从、也不提"不能对自己"
// （潜规则：这两条都靠目标高亮/可打出来体现）。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2QingJiaoRouSi : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型（技能）
    private const CardType type = CardType.Skill;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 自己失去的生命（无视格挡的真实 HP 损失，不随升级变化）
    private const decimal SelfHpLoss = 3m;

    // 自定义目标类型：所有「存活、与出牌玩家同阵营、但**不是玩家本人**」的生物
    //（= 队友 + 随从，独独排除自己 —— 这就是那条不写在卡面上的潜规则）。
    // 注册是确定性的（同 modId + 同 stem 永远得到同一个枚举值），所以写静态字段只注册一次即可。
    // 静态字段初始化发生在类型第一次被使用（= RitsuLib 扫描注册卡牌）之前，早于基类构造函数读取它。
    private static readonly TargetType OtherFriendlyTarget =
        CustomTargetType.RegisterSingleTargetType(Entry.ModId, "other_friendly_creature",
            static (creature, player) =>
                creature.IsAlive
                && player.Creature != null
                && creature != player.Creature
                && creature.Side == player.Creature.Side);

    // 卡图：暂时复用「辟谷丹」的立绘（文件名和类名不一致，所以写死路径）。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 数值：Heal = 治疗量（3，升级 +2 → 5）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Heal", 3m)
    ];

    // 关键字：消耗（升级后会被去掉，见 OnUpgrade）。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public lihuowang2QingJiaoRouSi()
        : base(energyCost, type, rarity, OtherFriendlyTarget, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 0. 先确定目标：必须是"另一个"友方单位。
        //    正常流程下没有合法目标时这张牌根本打不出来（RitsuLib 会把 CanPlay 判成 false），
        //    这里只是兜底：目标为空 / 指向自己 / 已死 → 整张牌直接不结算，
        //    免得白掉 3 点血却什么都没治到。**不要**退回给自己（那就是给自己加血了）。
        Creature? target = cardPlay.Target;
        if (target == null || target == Owner.Creature || !target.IsAlive)
        {
            return;
        }

        // 1. 自己先失去 3 点生命（Unblockable：无视格挡的真实 HP 损失）。
        //    放在最前面：血量不够会先死，后面的治疗/净化也就没意义了
        //   （与「大千录」那批自伤牌同一套写法）。
        await CreatureCmd.Damage(choiceContext, Owner.Creature, SelfHpLoss,
            ValueProp.Unblockable, Owner.Creature, this, cardPlay);

        if (Owner.Creature.IsDead)
        {
            return;
        }

        // 2. 回复生命
        await CreatureCmd.Heal(target, DynamicVars["Heal"].BaseValue);

        // 3. 再移除 1 层随机负面效果
        await RemoveOneRandomDebuff(target);
    }

    // 从目标身上的负面能力里随机挑一个，去掉 1 层。
    //   · 负面判定用引擎自己的口径：GetTypeForAmount(当前层数) == Debuff，
    //     所以"负数力量"这种也算负面（去掉 1 层 = 往 0 靠）；
    //   · 随机走玩家的 CombatCardSelection 随机流 —— 联机两端选中的是同一个，
    //     不会因为各端各自 Random 而导致状态分歧。
    private async Task RemoveOneRandomDebuff(Creature target)
    {
        if (Owner.Creature.Player is not { } player)
        {
            return;
        }

        List<PowerModel> debuffs = target.Powers
            .Where(p => p.IsVisible && p.GetTypeForAmount(p.Amount) == PowerType.Debuff)
            .ToList();

        if (debuffs.Count == 0)
        {
            return;
        }

        PowerModel chosen = player.RunState.Rng.CombatCardSelection.NextItem(debuffs)!;

        // 只削 1 层：还有剩余层数就 -1（ModifyAmount 到 0 会自己移除），最后一层则整个移除。
        if (chosen.Amount > 1m)
        {
            await PowerCmd.Decrement(chosen);
        }
        else
        {
            await PowerCmd.Remove(chosen);
        }
    }

    // 升级：治疗 3 → 5，并去掉「消耗」。
    // RemoveKeyword 是能删掉 CanonicalKeywords 里的关键字的：
    // 实例的 LocalKeywords 初始就是 CanonicalKeywords 的并集（见 CardModel.LocalKeywords），
    // AddKeyword / RemoveKeyword 都直接改这个集合，所以引擎认可"升级后不再消耗"。
    // 升级后的牌打完会正常进弃牌堆，之后还能再抽到。
    protected override void OnUpgrade()
    {
        DynamicVars["Heal"].UpgradeValueBy(2);
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
