using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace lihuowang2.Characters;

public sealed class lihuowang2CardPool : TypeListCardPoolModel
{
    // 卡框染色：战士风格但更偏鲜红（压低绿/蓝，避免砖红感）。
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateRgbShaderMaterial(0.96f, 0.14f, 0.12f);

    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "lihuowang2";
    public override string EnergyColorName => "lihuowang2";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://lihuowang2/... 里的 lihuowang2 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    // 铁甲参考值：DeckEntryCardColor=#D62000，EnergyOutlineColor=#802020
    public override Color DeckEntryCardColor => new(0.84f, 0.13f, 0f);
    public override Color EnergyOutlineColor => new(0.50f, 0.13f, 0.13f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}