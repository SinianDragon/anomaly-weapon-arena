# 反常武器竞技场 · v1 白盒（对应规格 v0.6）

引擎：Unity **6000.5.10f1** + URP，目标平台 WebGL。（规格写的是 6000.3.6f1，这台电脑只装了 6000.5，按讨论先沿用。）

## 一键生成

菜单 **Anomaly Arena → 1. Build Whitebox Scene**：生成图层（Wall / Character）、材质、效果数值资源、预制体、场景，并写好构建设置。
重新运行会覆盖场景和预制体；`Effects/` 里已经调过的数值资源**不会被覆盖**。

菜单 **Anomaly Arena → 2. Build WebGL**：输出到项目根目录 `Builds/WebGL/`。

## 目录

| 路径 | 内容 |
|---|---|
| `Scenes/Arena.unity` | 唯一场景：30 × 30 平台、墙、4 个缺口与 FallZone、各系统对象、玩家 |
| `Prefabs/` | Player、EnemySmall、EnemyLarge、Gun、Knife、Missile |
| `Effects/*.asset` | 六种效果的数值（ScriptableObject），**在 Inspector 里直接调** |
| `Scripts/Core/Interfaces.cs` | `IWeaponHolder`（能拿武器的人）、`IDamageDealer`、`IDamageReceiver`、状态枚举 |
| `Scripts/Characters/Combatant.cs` | 玩家和敌人的共同实现：状态机、刚体冲量撞飞、撞墙扣血、FallZone、保护、持有武器 |
| `Scripts/Characters/PlayerController.cs` / `Enemy.cs` | 玩家输入；敌人追击、蓄力（变黄）、攻击、掩体规则 |
| `Scripts/Arena/FallZone.cs` | 缺口外的触发器，碰到就判定掉下去 |
| `Scripts/Weapons/Weapon.cs` | 武器实例：类型、效果、揭晓、次数、调试强制效果 |
| `Scripts/Weapons/WeaponEffect.cs` | 效果基类、持续流程基类 `WeaponRuntime`、飞行物基类 `Projectile` |
| `Scripts/Weapons/Effects/` | 六个效果脚本，每种一个文件 |
| `Scripts/Weapons/Runtime/` | 子弹、飞刀、导弹、钩子、冲刺的运行逻辑 |
| `Scripts/Weapons/WeaponSpawner.cs` | 开局 3 把、每波后补 2 把、上限 5、效果概率、强制效果 |
| `Scripts/Game/` | GameManager（胜负、R 重开、统一清理）、WaveManager（波次、上限、红 X）、Hud（中文界面）、最低限度的反馈 |
| `Fonts/UIFont.ttf` | 思源黑体（SIL OFL）按游戏用到的字裁剪的子集 |

## 状态机

`CharacterState`：Normal / Knocked（被撞飞）/ Thrown（被扔出去）/ Hooked（被钩住）/ Dashing（冲刺中）/ Falling / Dead。
进入 / 退出写在 `Combatant.SetState`；钩子自己的阶段写在 `Hook.cs` 顶部注释里。

## 调数值、逐个测试

- 角色数值：选中 `Prefabs/` 里的预制体改 Inspector。
- 效果数值：选中 `Effects/` 里的资源。
- 规则（墙伤 5、撞墙速度阈值、撞飞衰减）：场景里的 GameManager。
- 波次、同屏上限、波间回血：WaveManager。
- 武器次数、效果概率：WeaponSpawner 的 `defs`。
- **强制效果**：运行时选中地上某把武器，把 `Debug Force Effect` 改掉（揭晓前生效）；或者在 WeaponSpawner 的 `Force On Spawn` 里让新生成的同类武器都用这个效果。
- 自动化测试用：`PlayerController.DebugSetAim(dir)` 锁定瞄准方向（运行时默认不开）。

## v0.6 之后的改动

- **导弹 · 追踪**：随机方向射出，1 秒后按权重随机锁定场上任意目标（玩家权重 3，每个敌人 1，在 `MissileHoming.asset` 里调）；目标没了就重新抽。
- **空手出拳**：左键出拳，伤害 3，击退 3 格（PlayerController 的 Punch 参数）。
- **大型敌人掉落**：被击败（打死或掉进缺口）时 50% 掉一把随机武器，不受地上 5 把的上限限制（WeaponSpawner）。
- **切波**：每波开始前玩家血量回满（WaveManager 的 `Refill Hp Each Wave`）；飞行中的子弹 / 飞刀 / 导弹、钩子、冲刺都不清掉，玩家照常移动。
- **界面**：换回上个版本的中文界面和思源黑体子集字体。注意这属于外部素材，规格写的是“只用 Unity 自带字体”，交作业前请和助教确认。

## 修改文字后要重新生成字体

字体只包含代码里用到的字，新增中文后要重新裁剪，否则新字显示成方块：

```bash
pip install fonttools
python3 - <<'PY' > chars.txt
import glob,string
s=set(string.printable)
for f in glob.glob('Assets/AnomalyArena/Scripts/**/*.cs',recursive=True)+glob.glob('Assets/AnomalyArena/Editor/*.cs'):
    s|=set(open(f,encoding='utf-8').read())
print(''.join(sorted(c for c in s if c not in '\r\n\t')))
PY
pyftsubset SourceHanSansCN-Medium.ttf --text-file=chars.txt --output-file=Assets/AnomalyArena/Fonts/UIFont.ttf
```

## 已验证

规格第 13 节 1–11 条都在编辑器里用脚本逐条跑过，结果与规格一致。第 12 条需要在浏览器里打开发布链接确认。

## 规格没写死、这里先这么做的地方

| 项目 | 现在的做法 |
|---|---|
| 玩家移动速度 | 6 格/秒（小型敌人 5.4，大型 3.6） |
| 导弹随机方向 | 完全 360° 随机；`MissileHoming.asset` 的 `Random Spread Degrees` 可改成瞄准方向左右一定角度 |
| 清掉飞行物 | 只在玩家死亡或胜利时清；切波不清（按新要求） |
| 反向射击的后坐力撞墙 | 算“推开”，不扣墙伤 |
| 钩子把大型敌人扔出去 | 也是 8 格（体型系数只影响“被撞飞”） |
| 保护期间挨打 | 伤害和撞飞一起无效 |
| 敌人自己走路 | 不会主动走下缺口，只有被撞飞 / 被扔才会掉 |
| 开局 | 有一个“点击开始”的标题画面（网页里需要先点一下获得焦点）；按 R 重开跳过它，直接从第 1 波开始 |
| 界面语言 | 中文，思源黑体子集（见上） |
| 补给 | 开局 3 把 + 每波后 2 把（上限 5）+ 大型敌人 50% 掉落；没有定时补给 |
