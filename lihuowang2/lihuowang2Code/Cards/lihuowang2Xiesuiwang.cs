using System;
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
using MegaCrit.Sts2.Core.ValueProps;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// 血衰王：消耗 1 张手牌，伤害 = 消耗堆里的诅咒数 × 每张诅咒的伤害（没有基础伤害）。
// 未升级：随机消耗；升级后：自己选一张。
[RegisterCard(typeof(lihuowang2CardPool))]
public class lihuowang2Xiesuiwang : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 每张诅咒的伤害（升级 +2）
    private const decimal DamagePerCurse = 5m;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 伤害 = 消耗堆里的诅咒数 × 每张诅咒的伤害。
    // 引擎的计算型变量固定按「CalculationBase + ExtraDamage × 乘数」算，所以这里
    // 把基础值留 0、把「每张诅咒的伤害」放在 ExtraDamage、乘数取消耗堆诅咒数，
    // 得到的就是纯粹的「诅咒数 × 每张伤害」（官方 BodySlam 这类完全没有基础值的牌也是这个写法）。
    // 好处：卡面描述能用 {CalculatedDamage} 显示算好的总伤害，战斗中还会带上力量/易伤等修正，
    // 并且实际打出的伤害和卡面预览走的是同一套计算。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new CalculationBaseVar(0m),   // 无基础伤害（引擎要求这个变量必须存在）
        new ExtraDamageVar(DamagePerCurse),   // 每张诅咒的伤害
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(CurseCountInExhaustPile)
    ];

    // 计算变量的乘数函数必须是静态的（引擎会拒绝捕获模型实例的委托），
    // 且只会在战斗中求值（不在战斗里时引擎会当作 0，不会调用这里）。
    private static decimal CurseCountInExhaustPile(CardModel card, Creature? _)
        => PileType.Exhaust.GetPile(card.Owner).Cards.Count(c => c.Type == CardType.Curse);

    public lihuowang2Xiesuiwang() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? player = Owner.Creature.Player;
        if (player == null)
            return;

        // 1. 消耗 1 张手牌。
        //    注意顺序：CardCmd.Exhaust 内部是先把牌放进消耗堆（CardPileCmd.Add(card, PileType.Exhaust, ...)）
        //    再返回，所以这里 await 完之后，刚消耗的这张牌已经在消耗堆里了 ——
        //    如果消耗掉的正好是诅咒，它会自然被算进下面的伤害里，不需要额外补算（补算反而会重复计一次）。
        List<CardModel> handCards = PileType.Hand.GetPile(player).Cards.ToList();
        CardModel? toExhaust = null;
        if (handCards.Count > 0)
        {
            if (IsUpgraded)
            {
                toExhaust = (await CardSelectCmd.FromHand(
                    choiceContext, player,
                    new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1, 1),
                    filter: null, source: this)).FirstOrDefault();
            }
            else
            {
                toExhaust = handCards[Random.Shared.Next(handCards.Count)];
            }
        }

        if (toExhaust != null)
            await CardCmd.Exhaust(choiceContext, toExhaust);

        // 2. 攻击：伤害交给计算变量（= 消耗堆诅咒数 × 每张诅咒伤害），此时消耗已经结算完，
        //    乘数取的是最新的消耗堆内容，所以"刚消耗的诅咒"也算进去了。
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    // 升级：每张诅咒的伤害 5 → 7（基础值保持 0，计算变量会自动重算）
    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(2m);
    }
}
