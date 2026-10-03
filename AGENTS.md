# Project Instructions

## Overview

This file is for project-specific instructions that DodWork and AI agents should follow when working in this workspace.

## Notes

- Add coding standards, commands, architecture notes, and workflow rules here.
- Keep instructions concise and update them when project conventions change.

## 模组约定：随从（`lihuowang2/lihuowang2Code/Minions/`）

**新增随从时，必须同时给出「血条可调」与「站位可调」，两者都要做成"改一个常量就能调"的形式。**

1. **血条长度**：在随从类里覆写 `HpBarSizeReduction`，**参数写成"目标血条宽度"再按公式反推**，
   不要写死缩减像素（写死的话，419 宽的立绘和 167 宽的立绘效果完全不同）：
   ```csharp
   private const float TargetHpBarWidth = 140f;   // 只调这个：想要的血条最终宽度（像素）
   public override float HpBarSizeReduction =>      // 引擎公式：血条宽 = hitbox 宽 + (24 − 本值)
       Math.Max(0f, SpritePixelWidth * BoundsWidthFactor + 24f - TargetHpBarWidth);
   ```
   （`SpritePixelWidth` = `GD.Load<Texture2D>(SpritePath)?.GetWidth() ?? 0f`，静态缓存；
   `BoundsWidthFactor = 1.1f` —— RitsuLib 贴图工厂把 `%Bounds` 设成"贴图尺寸 × 1.1"，所以默认 hitbox 宽 = 贴图宽 × 1.1，
   不是贴图宽本身。参考：`Minions/PengLongTeng.cs`。）
  - **hitbox / 血条 / power 行 / 名字条同源**，都从 `visuals/%Bounds` 推导
    （`NCreature.UpdateBounds` → `_stateDisplay.SetCreatureBounds`：血条以 hitbox 为中心、宽 = hitbox 宽 + (24 − 本值)；
    power 行宽 = hitbox 宽 + 25）。所以**只缩血条会让 power 行与血条错位**，要两者对齐就得收窄 `%Bounds`。
  - ⚠ **收窄 `%Bounds` 时必须同时把矩形中心补回来**（踩过坑）：
    RitsuLib 贴图工厂给的是 `Size = 贴图×1.1`、`Position.X = −宽/2`（以 x=0 居中），
    而引擎只取"矩形**左边缘** + 尺寸"当 hitbox，只改 `Size` 不动 `Position` → 中心左移 (原宽−新宽)/2，
    **hitbox/血条整体偏左、与立绘不居中**。正确写法：
    ```csharp
    float w0 = uiBounds.Size.X;
    float w1 = Math.Min(TargetHpBarWidth, w0);
    uiBounds.Size = new Vector2(w1, uiBounds.Size.Y);
    uiBounds.Position += new Vector2((w0 - w1) * 0.5f, 0f);   // 左边缘右移半个差值 ⇒ 中心原地不动
    ```
    （收窄会同时缩小可点/悬浮区域；若立绘左右透明边距不对称可再加一个 ±nudge 常量微调。）
    参考：`Minions/PengLongTeng.cs` 的 `StretchUiBounds`。



2. **站位距离**：新建 `<随从名>Layout.cs`（实现 `MinionLib.Layout.IMinionLayout`），注册**必须幂等且保证早于 `Rearrange`**：
   - 写 `EnsureLayoutRegistered()`（静态 bool 守卫 + `MinionLayoutManager.Register(new XxxLayout(), priority: 20)`），
     在**静态构造**（最早时机）与 **`OnSummon`**（`MinionCmd.AddMinion` 的顺序是
     `AddPet → OnSummon → MinionAnimCmd.Rearrange()`，所以这里一定早于摆位）各调一次；
     **只靠静态构造不可靠** —— 不保证在 `Rearrange` 之前跑过，摆位会静默不生效；
   - priority 必须是 20：`TestMinionLayout`(10) 会认领"所有随从"，本布局必须先跑
     （已写入 `context.Positions` 的节点不会被后续布局处理）；
   - 布局里用 `DefaultMinionLayout.CalculateMinionPositions` 取基准坐标，再推 `ExtraDistance` 像素；
     **方向必须相对玩家节点判断**（`ownerNode.Position.X` 比较：`position.X < ownerX ? -1 : +1`），
     **不要用 `position.X >= 0`** —— 玩家在房间坐标系里是负 X（本作如此），
     用正负号会把"玩家前方"的随从误判成后方，于是往玩家那侧推（方向反了）；
   - **只把本随从的节点写进 `context.Positions`**，其它随从留给后续布局。
   参考：`Minions/PengLongTengLayout.cs`、`Minions/QiuChiBaoLayout.cs`。

3. **召唤血量做成参数**：血量写在**召唤牌**上（`private const int MinionHp = 20;`），通过
   `MinionSummonOptions(MaxHp: MinionHp, ...)` 传进召唤流程；卡面文字用动态变量 `{MinionHp}` 显示
   （`CanonicalVars => [new DynamicVar("MinionHp", MinionHp)]`），这样改一个数字，行为与卡面一起变。
   - ⚠ **MinionLib 不会自动应用 `MaxHp`**：`MinionSummonOptions` 的 `MaxHp`/`PrimaryStatAmount` 等字段
     只是透传给 `OnSummon`，库里没有任何消费点 —— 必须在随从的 `OnSummon` 里自己落地：
     `decimal gained = await CreatureCmd.SetMaxHp(Creature, maxHp); if (gained > 0m) await CreatureCmd.Heal(Creature, gained);`
     （= 官方 `OstyCmd` 用的 `CreatureCmd.GainMaxHp` 的语义：加最大生命并回等量血）。
   - 随从类仍必须有 `MinInitialHp/MaxInitialHp`（`MonsterModel` 的抽象成员，建怪时用），
     把它们设成与召唤牌一致的**兜底默认值**即可（召唤牌的参数会覆盖它）。
   - 手写的 `LocString`（例如卡牌自己 new 出来的随从 hover 文案）**不会**自动拿到动态变量，
     要用 `loc.Add("MinionHp", MinionHp)` 手动塞，否则卡面会原样显示 `{MinionHp}`。
   参考：`Cards/lihuowang2XiuZhenPengLongTeng.cs`、`Minions/PengLongTeng.cs` 的 `OnSummon`。

4. 承伤型随从（守护）：召唤时须指定 `MinionPosition.Front` 并挂 `MinionLib.Powers.MinionGuardianPower`
   （该能力内部要求 `Position == Front` 才生效）；同时应把血条接到主人的格挡上，与奥斯提一致：
   `NCombatRoom.Instance?.GetCreatureNode(Creature)?.TrackBlockStatus(owner.Creature);`
   （打向宠物的伤害由主人的格挡池吸收，所以血条要显示共享格挡）。参考：`Minions/PengLongTeng.cs` 的 `OnSummon`。

## 模组约定：新增卡牌（`lihuowang2/lihuowang2Code/Cards/`）

1. **注册**：`[RegisterCard(typeof(lihuowang2CardPool))]` + `ModCardTemplate(费用, 类型, 稀有度, 目标类型, 显示在卡池)`；
   卡池是 `TypeListCardPoolModel`，按特性自动收录，不需要改别处。卡图按类名取
   `images/cards/<类名>.png`（`CardAssetProfile`，缺图会回落占位图）。
2. **本地化**：键 = `LIHUOWANG2_CARD_` + 类名的 SCREAMING_SNAKE
   （`lihuowang2DingShenFu` → `LIHUOWANG2_CARD_LIHUOWANG2_DING_SHEN_FU`），
   `zhs/cards.json` 与 `eng/cards.json` 各写 3 条：`title` / `description` / `smartDescription`（后两者内容一致）。
   - ⚠ **描述里的换行必须在 JSON 里是转义 `\n`（单个反斜杠）**：用编辑工具时若写成双反斜杠，
     游戏会把 `\n` 原样显示出来。改完务必复核：
     `$j.'<key>.description'.Contains([char]10)` 应为 True、`.Contains([char]92)` 应为 False。
3. **悬停**：描述里出现的概念都要给 hover —— 官方能力 `HoverTipFactory.FromPower<T>()`、
   官方卡 `FromCard<T>()` / `FromCardWithCardHoverTips<T>()`、遗物 `FromRelic<T>()`、
   非模型概念 `HoverTipFactory.Static(StaticHoverTip.X)`；
   「击晕」用 `StunIntent.GetStaticHoverTip()`（官方 `Whistle` 同款，图标走常驻 intent_atlas，
   不要自己用 PreloadManager 取图，否则换房间时会被卸载）。
4. **「非 BOSS 单位」要用两条信号判，不能只看房间**：引擎没有"某只怪是 boss"的标记
   （`MonsterModel` 没这个字段，`Creature.StunInternal` 也不拦 BOSS —— 官方 `Whistle` 就是无条件击晕）。
   实测**只按房间判会漏**（BOSS 出现在非 BOSS 房——调试命令直接生成、事件里刷出来——就会把 BOSS 晕住）。
   两条信号满足任一条即算 BOSS：
   - **A 房间**：`Owner.Creature.CombatState?.Encounter?.RoomType == RoomType.Boss`
     （`MegaCrit.Sts2.Core.Rooms`；`CombatRoom.RoomType => Encounter.RoomType`，「监天司令牌」判 BOSS 档赏金同理）；
   - **B 怪本身**：目标是本幕 BOSS 的那几只怪 ——
     `Owner.RunState?.Act` 的 `BossEncounter` / `SecondBossEncounter` 的 `AllPossibleMonsters`
     按怪物 `Id`（`ModelId`，用 `.Equals` 走值比较）比对。
     已核对官方数据：真实 BOSS 的怪都只出现在 BOSS 遭遇里，不会误伤普通怪（只有测试用的 `MockBossEncounter`/`BigDummy` 复用）。
   代价：BOSS 房里的杂兵也判成 BOSS（宁可少晕，不可晕错）。参考：`Cards/lihuowang2DingShenFu.cs`（定身符）。
5. **保留/消耗等关键字**只写进 `CanonicalKeywords` 即可：引擎的 `ShouldRetainThisTurn`、卡面文字
   （`CardKeywordOrder.beforeDescription/afterDescription`）都自动读它，不需要额外代码或本地化文本。
