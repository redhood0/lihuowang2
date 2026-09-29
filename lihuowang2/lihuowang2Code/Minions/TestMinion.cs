using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MinionLib.Layout;
using MinionLib.Minion;
using MinionLib.Powers;
using lihuowang2.RitsuAdapters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace lihuowang2.Minions;

// 测试随从：曹操。生命 10。
// 基类用的是 MinionLib 的 Source Generator 产出的 RitsuLib 适配类（lihuowang2.RitsuAdapters.ModMinionTemplate），
// 注册沿用项目里其它内容一样的 [RegisterMonster]（RitsuLib 自动注册）。
// 行为：MinionLib 的 MinionModel 自带「MINION_IDLE」状态机 → 随从不会自行行动，需要卡牌/Action 去指挥它。
[RegisterMonster]
public sealed class TestMinion : ModMinionTemplate
{
    // 注册自定义摆位（把曹操放得离角色更远一点）。静态构造只跑一次；
    // 用它的时机很安全：布局必须在 MinionAnimCmd.Rearrange() 之前注册好，
    // 而那时一定已经访问过本类型（PlayerCmd.AddPet<T> 会先取 ModelDb.Monster<TestMinion>()）。
    static TestMinion()
    {
        MinionLayoutManager.Register(new TestMinionLayout(), priority: 10);
    }

    // 初始生命值。两者都为 10 时，召唤出来的血量是确定的 10（引擎在 [Min, Max] 区间取值）。
    public override int MinInitialHp => 10;

    public override int MaxInitialHp => 10;

    // 纯静态 PNG 形象的贴图路径（放在模组 images/minions/ 下，已由 Godot 导入 → 会打进 pck）。
    private const string SpritePath = "res://lihuowang2/images/minions/testminion.png";

    // 视觉资源（回落用）：RitsuLib 的视觉路径既收 PackedScene，也收 Texture2D。
    // 因为下面重写了 TryCreateCreatureVisuals()，正常情况下走的是代码构造那条路。
    public override MonsterAssetProfile AssetProfile => new(SpritePath);

    // 用一张贴图直接构造随从视觉，不需要 .tscn 场景。
    // 引擎里生物的原点在"脚底"，而 Sprite2D 默认以图片中心对齐原点 →
    // 这里按图片高度上抬一半，让图片底部正好站在脚下（想要不同高度就改 LiftRatio）。
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        Texture2D? texture = GD.Load<Texture2D>(SpritePath);
        if (texture == null)
        {
            // 贴图加载失败时返回 null，回落到 AssetProfile / 引擎默认视觉。
            return null;
        }

        const float LiftRatio = 0.5f; // 0.5 = 抬半个图片高度（脚底对齐）
        VisualNodeStyle style = VisualNodeStyle.Create(
            position: new Vector2(0f, -texture.GetHeight() * LiftRatio));

        return RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(texture, style);
    }

    // 名字（本地化表 monsters，键名写死以便和本地化文件一一对应；
    // 官方 BigDummy 也是这种写法：L10NMonsterLookup("BIG_DUMMY.name")）。
    public override LocString Title =>
        MonsterModel.L10NMonsterLookup("LIHUOWANG2_MONSTER_TEST_MINION.name");

    // 召唤时挂「守护」：把本该打在宠物主人（玩家）或更靠后宠物身上的未格挡伤害，转移到这只随从身上。
    // 为什么不能靠"被怪物选中"：引擎的怪物 AI 只以 PlayerCreatures（= Creatures.Where(c => c.IsPlayer)）
    // 为候选目标，随从不在其中，所以原版 Osty 也是靠这种伤害转移来承伤的。
    // 前置条件（MinionGuardianPower 内部判定）：随从必须站在 MinionPosition.Front —— 召唤牌已指定 Front。
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner,
        MinionSummonOptions options)
    {
        await PowerCmd.Apply<MinionGuardianPower>(choiceContext, Creature, 1m, owner.Creature,
            options.Source);
    }
}
