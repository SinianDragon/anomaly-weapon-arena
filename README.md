# Anomaly Weapon Arena（反常武器竞技场）

俯视角竞技场原型：捡起外形熟悉、功能要第一次用了才揭晓的武器，清完三波敌人，或者把它们打进缺口。

- 在线试玩（GitHub Pages）：https://joewenbozhang-blip.github.io/anomaly-weapon-arena/
- 引擎：Unity 6000.5.10f1 + URP，目标平台 WebGL

## 打开工程

1. 用 Unity Hub 添加这个文件夹，编辑器版本选 6000.5.10f1。
2. 打开 `Assets/AnomalyArena/Scenes/Arena.unity`，点 ▶ 运行。
3. 需要重建场景或打包时，用菜单 **Anomaly Arena → 1. Build Whitebox Scene / 2. Build WebGL**。

代码结构、调数值的方式和待定项的暂定处理见 [`Assets/AnomalyArena/README.md`](Assets/AnomalyArena/README.md)。

## 仓库内容

只收录游戏本身（`Assets/AnomalyArena/`）、它必需的渲染与输入设置（`Assets/SourceFiles/Settings`、`Assets/SourceFiles/InputSystem`）、`Packages/` 和 `ProjectSettings/`。
网页版构建放在 `gh-pages` 分支。
