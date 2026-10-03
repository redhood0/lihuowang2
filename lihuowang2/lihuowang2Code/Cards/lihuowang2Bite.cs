using lihuowang2.Characters;
using lihuowang2.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Cards;

// RegisterCard 会把这张牌交给 RitsuLib 自动注册。
// RegisterCharacterStarterCard 会把它追加进 lihuowang2Character 的初始卡组。
[RegisterCard(typeof(lihuowang2CardPool))]
[RegisterCharacterStarterCard(typeof(lihuowang2Character), 1)]
public class lihuowang2Bite: ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 0;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Common;
    // 目标类型（AnyEnemy表示任意敌人）
    private const TargetType targetType = TargetType.AnyEnemy;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;

    // 攻击段数：基础 1 段；处于「疯癫」时额外多打 1 段（共 2 段）。
    private const int baseHitCount = 1;
    private const int maniaBonusHitCount = 1;

    // 命中特效。咬类攻击在引擎里用的就是 vfx/vfx_bite（斩类才是 vfx/vfx_attack_slash）。
    private const string hitFxPath = "vfx/vfx_bite";

    // 卡图资源。
    // 如果你按这行代码写，文件名就对应 lihuowang2/images/cards/lihuowang2Strike.png。
    // 这里的 res://lihuowang2/... 是 Godot 资源路径，对应的是你的资源文件夹名字。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
        // 卡框等，有需求自己添加。需要自行判断卡牌类型（攻击、技能、能力等）设置，建议写在基类里。
        // 如果使用自定义卡池，需要改下material，看添加人物章节的添加卡池部分
        // FramePath: "", // 卡牌背景
        // PortraitBorderPath: "", // 边框（状态牌感染使用的）
        // BannerTexturePath: "" // 横幅（不同类型）
    

    // 卡牌基础数值（基础伤害）。升级后 +1（4 → 5）。
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(4, ValueProp.Move)
    ];
    
    // 关键字：保留、消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Retain
    ];

    public lihuowang2Bite() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 打出时的效果逻辑。
    // 与官方 TwinStrike（双重打击）同款的多段写法：先把段数算出来，再交给 WithHitCount
    // 在一次攻击命令里连打，而不是循环调用多次 DamageCmd.Attack（那样会播多遍完整攻击动作）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 若攻击者（Owner）处于疯癫状态，则额外攻击一次（共攻击 2 次）
        int hits = baseHitCount + (Owner.Creature.HasPower<CrazyPower>(1) ? maniaBonusHitCount : 0);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(hitFxPath)
            .Execute(choiceContext);
    }

    // 升级后的效果逻辑：获得「保留」；基础伤害 4 → 5（疯癫加成随之变为 +5）
    protected override void OnUpgrade()
    {
        
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}