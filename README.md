# Anomaly Weapon Arena

A top-down arena prototype: pick up weapons that look familiar but only reveal what they do the first time you use them, then clear three waves of enemies or knock them into the gaps.

- Play online (GitHub Pages): https://siniandragon.github.io/anomaly-weapon-arena/
- Engine: Unity 6000.6.3f1 + URP, target platform WebGL
- All in-game text is in English; the title screen can switch between whitebox and illustrated mode (ART button or the T key). Whitebox is the default.

## Opening the project

1. Add this folder in Unity Hub and choose editor version 6000.6.3f1.
2. Open `Assets/AnomalyArena/Scenes/Arena.unity` and press ▶.
3. To rebuild the scene or make a build, use the menu **Anomaly Arena → 1. Build Whitebox Scene / 2. Build WebGL**; to apply the latest changes without rebuilding the scene, use **3. Update Existing Scene**.

Code structure, how to tune values, and how open questions are currently handled are described in [`Assets/AnomalyArena/README.md`](Assets/AnomalyArena/README.md).

## What is in the repository

Only the game itself (`Assets/AnomalyArena/`), the rendering and input settings it needs (`Assets/SourceFiles/Settings`, `Assets/SourceFiles/InputSystem`), `Packages/` and `ProjectSettings/`.
The web build lives on the `gh-pages` branch.
