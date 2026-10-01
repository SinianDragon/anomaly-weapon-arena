# Anomaly Weapon Arena · 1.0 (whitebox + illustrated mode; based on spec v0.6, later changes are listed per version below)

Engine: Unity **6000.5.10f1** + URP, target platform WebGL. (The spec says 6000.3.6f1; this machine only had 6000.5 installed, so that was used as agreed. The v0.7 changes were made and verified on 6000.6.3f1.)

## One-click build

Menu **Anomaly Arena → 1. Build Whitebox Scene**: creates the layers (Wall / Character), materials, effect value assets, the art set, prefabs and the scene, and writes the build settings.
Running it again overwrites the scene and prefabs; value assets in `Effects/` that have already been tuned are **not overwritten**.

Menu **Anomaly Arena → 2. Build WebGL**: outputs to `Builds/WebGL/` in the project root.

Menu **Anomaly Arena → 3. Update Existing Scene**: does not rebuild the scene; it only refreshes the English names / descriptions of the effects, generates `Art/ArtSet.asset` and assigns it to the GameManager, switches the enemy prefabs to the close-range punch values, shrinks the large enemy to twice the small one, and sets weapon supply to 3 / 4 / 5 per wave (cap 6).

## Folders

| Path | Contents |
|---|---|
| `Scenes/Arena.unity` | The only scene: 30 × 30 platform, walls, 4 gaps with FallZones, the system objects, the player |
| `Prefabs/` | Player, EnemySmall, EnemyLarge, Gun, Knife, Missile |
| `Effects/*.asset` | Values of the six effects (ScriptableObject), **tuned directly in the Inspector** |
| `Scripts/Core/Interfaces.cs` | `IWeaponHolder` (anything that can hold a weapon), `IDamageDealer`, `IDamageReceiver`, state enums |
| `Scripts/Characters/Combatant.cs` | Shared implementation for the player and enemies: state machine, rigidbody-impulse knockback, wall damage, FallZone, protection, holding a weapon |
| `Scripts/Characters/PlayerController.cs` / `Enemy.cs` | Player input; enemy chase, wind-up (turns yellow), attack, cover rule |
| `Scripts/Arena/FallZone.cs` | Trigger outside each gap; touching it counts as falling |
| `Scripts/Weapons/Weapon.cs` | Weapon instance: type, effect, reveal, uses, debug forced effect |
| `Scripts/Weapons/WeaponEffect.cs` | Effect base class, base class for ongoing routines `WeaponRuntime`, projectile base class `Projectile` |
| `Scripts/Weapons/Effects/` | The six effect scripts, one file each |
| `Scripts/Weapons/Runtime/` | Runtime logic for the bullet, throwing knife, missile, hook and dash |
| `Scripts/Weapons/WeaponSpawner.cs` | 3 weapons at start, 2 more after each wave, cap 5, effect chances, forced effect |
| `Scripts/Game/` | GameManager (win / lose, restart with R, unified cleanup, whitebox / illustrated switch), WaveManager (waves, cap, red X), Hud (English UI), minimal feedback |
| `Scripts/Art/` | `ArtSet` (texture table for illustrated mode), `Art` (billboards / flat textures / floor and gap decoration / stretching hook blade) |
| `Art/Textures/` | Illustrated-mode textures (PNGs cut from two source images); `Art/ArtSet.asset` ties them together |

## State machine

`CharacterState`: Normal / Knocked (knocked back) / Thrown (thrown by the hook) / Hooked / Dashing / Falling / Dead.
Enter / exit logic is in `Combatant.SetState`; the hook's own phases are described in the comment at the top of `Hook.cs`.

## Tuning values and testing one thing at a time

- Character values: select the prefab in `Prefabs/` and edit it in the Inspector.
- Effect values: select the asset in `Effects/`.
- Rules (wall damage 5, wall-hit speed threshold, knockback damping): the GameManager in the scene.
- Waves, max alive, healing between waves: WaveManager.
- Weapon uses and effect chances: `defs` on the WeaponSpawner.
- **Forced effect**: at runtime, select a weapon on the ground and change `Debug Force Effect` (works before reveal); or set `Force On Spawn` on the WeaponSpawner so every newly spawned weapon of that type uses the effect.
- For automated tests: `PlayerController.DebugSetAim(dir)` locks the aim direction (off by default at runtime).

## Changes after v0.6

- **Missile · Homing**: launched in a random direction; after 1 second it locks onto a weighted-random target on the field (player weight 3, each enemy 1, tuned in `MissileHoming.asset`); if the target is gone it picks again.
- **Unarmed punch**: left click to punch, damage 3, knockback 3 units (Punch parameters on PlayerController).
- **Large-enemy drops**: when defeated (killed or knocked into a gap) there is a 50% chance to drop a random weapon, ignoring the cap of 5 weapons on the ground (WeaponSpawner).
- **Wave change**: the player's HP is refilled before each wave (`Refill Hp Each Wave` on WaveManager); bullets / knives / missiles in flight, hooks and dashes are not cleared, and the player keeps moving.

## v0.7 changes (playtest feedback)

### 1. Enemies: punch only at close range, and the punch can be dodged

- **Before**: an enemy stopped and wound up at 1.5 units from the player's body edge (2 for large) and dealt damage by distance alone when the wind-up ended. It was very hard to dodge and too easy to lose HP.
- **Now**: it keeps walking until it almost touches the player (edge distance ≤ `triggerRange`: 0.3 small, 0.4 large) before winding up. During the wind-up the body flashes yellow, **facing is locked**, and it stands still.
- After the wind-up it swings an arc along the locked direction (`attackArc` 120°, hit distance `attackRange`: 0.8 small, 1.0 large, from the body edge), and a red sector flashes on the ground. If the player has already left the arc, it misses.
- Wind-up time `windupTime`: 0.5 s small, 0.8 s large. The player moves at 6; with a 0.2 s reaction after seeing yellow, the remaining 0.3 s covers 1.8 units, enough to leave the hit area.
- The cover rule is unchanged: a hooked enemy inside the punch arc is hit first.
- All values are on the Enemy component of `Prefabs/EnemySmall` and `EnemyLarge`.

### 2. Gun · Swing → Charge Swing

- **Before**: a click produced a sector, like the unarmed punch but slightly larger, and the two felt the same.
- **Now**: **hold the left button to charge, release to swing**.
  - While charging, movement speed × 0.4 (`chargeMoveMultiplier`); an orange-red sector preview on the ground grows with the charge and blinks when full; the bottom panel shows a charge bar.
  - Tap: radius 2.5, 90°, damage 6, knockback 5.
  - Full charge (1 s): radius 5, 150°, damage 15, knockback 10. In between it is interpolated linearly by charge ratio.
  - Being knocked back / hooked / killed while charging interrupts it with no swing; the use is still spent (it is spent on press).
- Values are in `Effects/GunSwing.asset`.
- Difference from the unarmed punch: the punch is an instant small white sector (1.2 units, damage 3); the Charge Swing is orange-red and can reach more than 3 times the punch range.

### 3. Knife · Throwing Knife: it has to look thrown

- Playtest problem: the throwing knife and the hook are both 50%, but in whitebox the knife was a flat box with a long 0.35 s trail that flew out and back, and it looked almost the same as the hook's 'blade extends and retracts', so the knife seemed to have only one function.
- Now the knife **spins fast** in the air (reversed on the way back) and the trail is shortened to 0.08 s; in illustrated mode it is a spinning knife texture. The hook is still a blade extending from the hand.
- The effect logic itself is unchanged. Both effects were forced and tested once each in the editor (see 'v0.7 verified' below).

### 4. English UI

- All in-game text (title, HUD, hints, weapon names and effect descriptions, end screen) is in English, using Unity's built-in font `LegacyRuntime.ttf`; the Source Han Sans subset (`Fonts/`) was removed. This also goes back to the spec's 'only Unity's built-in font' requirement.
- Weapon names: Gun / Knife / Missile; effect names: Charge Swing, Reverse Shot, Throwing Knife, Hook, Homing, Launch Yourself.

### 5. Whitebox / illustrated switch

- The title screen has two buttons: **START** and **ART: WHITEBOX / ART: ILLUSTRATED** (keyboard: Space to start, T to switch). **Whitebox unless switched.**
- Switching reloads the scene and every object is created in the new mode; the flag is a static variable and is kept after restarting with R. It can only be switched on the title screen, not during play.
- What illustrated mode replaces:

| Whitebox | Illustrated |
|---|---|
| Blue player capsule | Blue blob billboard |
| Red small / large enemy capsules | Red small monster / big red monster billboards; flash yellow during wind-up |
| Gun / knife / missile boxes on the ground | Rifle / knife / RPG billboards; lying flat and pointing along the aim direction when held |
| Thin box extending from the hook | Hilt + stretchable middle + tip (assembled from the longsword in the source art) |
| Throwing knife box | Spinning knife |
| Homing missile cylinder | Missile + exhaust flame |
| Orange afterimage of Launch Yourself | Missile flame behind the player |
| Carried corpse capsule | Enemy texture laid flat and greyed |
| Grey floor | Tiled sand texture (3 units per tile) |
| Black line on gap edges | A row of 'sand edge + black pit' outside each gap |

- Walls, bullets, explosions, the red spawn X and sector hints still use the whitebox look in illustrated mode (the source art has no matching images).
- All textures are camera-facing billboards or flat quads using the FxTransparent material (URP Unlit transparent); sizes are tuned in `Art/ArtSet.asset`.
- Duplicate images in the source art (second big red monster, second / third missile, second knife) are not used.

## Verified

Items 1–11 of spec section 13 were each run by script in the editor and matched the spec. Item 12 needs to be confirmed by opening the published link in a browser.

## v0.7.1 changes (second round of playtest feedback)

1. **Melee hits one target**: the unarmed punch and Gun · Charge Swing both hit only the **nearest character** in the sector (`Query.MeleeTarget`); the sector only decides reach. Enemy punches already hit only the player (or the cover in front).
2. **Readability of illustrated mode**: the floor is now a desaturated, low-contrast, darkened sand (`Art/Textures/floor_sand_muted.png`), 5 units per tile; ground weapon labels moved above the texture on a dark plate; enemy HP bars sit on top of the billboard; the wave banner and hint text have dark backing. Gap edges keep their bright yellow, which works as a danger cue.
3. **Sizes** (`Art/ArtSet.asset`): player billboard height 1.4, small 1.5, large 3; ground weapon width 3, held 2.2, so weapons stand out more than characters.
4. **Large enemy = twice the small one**: radius 1.5 → 1, height 3 → 4 (small: radius 0.5, height 2), red spawn X 3 → 2.4. Whether an enemy is large is now decided by the `large` checkbox on Enemy, not by radius.
5. **Weapon supply**: added at the start of each wave: 3 for wave 1 (game start), 5 for wave 2, 8 for wave 3 (`perWaveCounts` on WeaponSpawner); ground cap 5 → 12, otherwise wave 3 does not fit. The 50% large-enemy drop is unchanged.

## v0.7.2 changes: hit feedback

Being hit used to show only a small translucent sphere flash that was barely visible. Now every damage path (attack, wall, killed by the human missile) goes through `Combatant.PlayHitFeedback`:

| Layer | What happens |
|---|---|
| White flash | Turns pure white for 0.1 s (whitebox changes color; illustrated mode pushes the texture color to saturation) |
| Squash and recover | Returns from '+35% horizontal, −35% vertical' to normal within 0.15 s |
| Shards | A handful of small cubes fly in the hit direction, fast then slow, shrinking as they go |
| Flash / shock ring | The hit point lights up; heavy hits spread a shock ring on the ground |
| Damage number | '-5' floats up and fades, scaled up when it appears; heavy hits and hits on the player use large text, red for the player |
| Hit stop | 0.035 s normal, 0.08 s heavy (time scale 0.05), applied immediately on call |
| Screen shake | 0.5 units when the player is hit, 0.25 units for a heavy hit on an enemy |

By damage type (`HitKind`):

| Type | Source | Shards |
|---|---|---|
| Blunt | Punch, swing, enemy punch | Warm yellow, 120° fan |
| Bullet | Reverse Shot | Bright yellow, narrow and fast |
| Pierce | Throwing knife, hit by the human missile | Cyan-white, narrowest and fastest |
| Slam | Wall impact, hit by a carried corpse | Grey-white, all directions, counts as heavy |
| Blast | Missile explosion | Orange, all directions, counts as heavy |

Damage ≥ 10 also counts as heavy. All feel values are under **Feedback** on the GameManager in the scene.

## v1.0: all illustrated-mode assets wired in

Version 1.0.0 (`ArenaSetup.Version` → PlayerSettings.bundleVersion, shown in the bottom-right corner of the title screen).

| Where | Illustrated look | Asset |
|---|---|---|
| Enemy wind-up | Switches to a glowing version with a yellow halo and blinks (used to be a yellow tint) | `enemy_small_windup` / `enemy_large_windup`: made from the base image plus a halo, same margin on every side, so it stays concentric with the base when scaled |
| Enemy punch | An arc flash in front of the fist | `enemy_punch_arc` |
| All hits | Hit spark (used to be a translucent sphere); dust at the feet on wall impact / corpse hit | `hit_spark`, `dust` |
| 0.5 s protection after a hit | A bubble around the body, blinking for the last 0.15 s (translucent blue sphere in whitebox) | `shield_bubble` |
| Homing missile explosion | 3-frame fireball animation + a ground ring expanding to the real 3-unit radius | `explosion_0..2`, `explosion_ring` |
| Launch Yourself | A larger rocket flame behind the player | `rocket_flame` |
| Reverse Shot | Bullet with a trail | `bullet` |
| Corpse carried by the throwing knife | Face down with the knife in its back | `corpse_small` / `corpse_large` |
| Knife · Hook | Chain blade: hilt + chain tiled per link + a knife (links never stretch however far it extends) | `hook_hilt`, `hook_mid` (Repeat), `hook_tip` |
| Spawn warning | Red X texture (large is twice the small one) | `spawn_x_small` / `spawn_x_large` |
| Walls | Brick wall; tops tiled by xz, sides by 'distance along the wall × height' | `wall_brick` (Repeat) |
| Sprint | Dust at the feet | `dust` |

- When an entry in `ArtSet` is left empty, that element automatically falls back to its whitebox look.
- New Edit Mode test `ArtSetTest` (7 tests): every texture is assigned, the explosion has 3 frames, the glow images have the same margin on every side (required for alignment), and textures that need tiling are imported as Repeat.
- **31 tests** in total (Play Mode 24 + Edit Mode 7), all passing.
- The small monster's wind-up glow does not use the Gemini image (its glossy 3D style did not match the other art); it is generated from the base image instead, and the big monster is done the same way so the two are consistent and aligned.

## v0.8 changes: weapon feel, sprint, irregular arena, automated tests

### Weapons and movement

| Item | Now |
|---|---|
| Gun · Charge Swing | Back to **area damage**: everyone in the sector is hit (`Query.InSector`). The unarmed punch still hits only the nearest |
| Gun · Reverse Shot | **Hold the left button to keep firing** (cooldown 0.12 s); one use = a 5-round clip, 30 rounds over 6 uses; each shot's recoil pushes 0.8 units and stacks, applied through `Push` on top of movement without entering the knocked state; bullet hits have no hit stop or screen shake |
| Knife · Throwing Knife | After hitting a character it bounces to the nearest next character within 7 units with no wall in between, **up to 4**, then flies back; **it cannot be thrown again until it returns** (`Weapon.InFlight`). The first enemy killed is carried back with it |
| Missile · Launch Yourself | **Stops at a wall**, takes wall damage once (5) and is bounced back 3 units (`SlamIntoWall`); it no longer reflects and keeps flying |
| Sprint | Hold **Shift** while moving: speed ×2; full stamina gives 1 second of sprint and refills in 3 seconds when not sprinting; after running out it must recover to 25% before sprinting again (`Stamina`, tuned on the player prefab). A stamina bar is shown at the top left |

### Arena: irregular polygon

- The shape data is in `Scripts/Arena/ArenaShape.cs`: 13 edges, with chamfered corners and two edges that bend inward. The bounding area is about the same as the original 30 × 30, so the camera did not move.
- 5 gaps: 3 large gaps of 7 units on the north, south and **east (new)**; 2 small gaps of **1.6 units** on the east and west (was 2.5). The small gaps are wider than the player and small enemies (diameter 1) and narrower than large enemies (diameter 2), so large ones get stuck.
- Everything in the game logic that assumed a square now uses `ArenaShape`: enemies do not walk off the edge (they slide along it), random weapon positions, enemy spawn points, the hook's pull limit, weapon drop positions.
- The floor, walls and gaps in the scene are generated by `Editor/ArenaGeometryBuilder.cs`: the floor is a polygon mesh (UV = world coordinates, textures tile per unit); walls are built along the outside of the edges and mitered at the corners; meshes are stored in `Scenes/ArenaGeometry.asset`. After changing `ArenaShape`, run menu **5. Rebuild Arena Geometry** to rebuild only the arena and leave other objects alone.

### Engineering

- Code is split into assemblies: `AnomalyArena` (`Scripts/`), `AnomalyArena.Editor` (`Editor/`), `AnomalyArena.Tests` (`Tests/Runtime/`, Play Mode tests), `AnomalyArena.Editor.Tests` (`Tests/Editor/`).
- Test framework: Unity Test Framework + [TestHelper](https://github.com/nowsprinting/test-helper) 1.6.4 (OpenUPM, via scopedRegistries in `manifest.json`).
- **24 automated tests**, all passing: stamina (5), knife ricochet and 'cannot throw again before it returns' (3), Charge Swing range (2), punch single-target / sector check (2), human missile wall hit (1), Reverse Shot clip and not interrupting input (2), per-wave supply counts (3), arena shape (6, including 'small gap wider than a small enemy, narrower than a large one').
- Running: the Test Runner window, or menu **4. Run Play Mode Tests**; results are written to `Logs/TestResults.xml` and `Logs/TestResults.txt`.

### v0.7.1 / v0.7.2 verified (Play mode driven by script in the editor)

| Item | Result |
|---|---|
| Supply counts | Waves 1 / 2 / 3 = 3 / 5 / 8, cap 12, 3 on the ground at start |
| Punch with two enemies in range | Only the nearer one is selected |
| Full Charge Swing with one enemy at 3 units and one at 4 | The one at 3 units dies, the one at 4 keeps HP 10/10 |
| Large enemy prefab | Collider radius 1, height 4, `large` = true |
| Each of the 5 damage types on one enemy | 71 effect objects created; flash applied; squashed to (1.31, 0.69, 1.31) |
| Player hit | Camera offset 0.364 units |
| After 1.2 s | Flash cleared, scale restored, camera back in place, all effect objects gone, time scale 1 |
| Hit stop | Time scale 0.05 on the hit frame, still 0.05 on the next frame, back to 1 afterwards |
| Illustrated-mode screenshot (with UI) | Floor is dark, weapon labels are above the textures, the banner has backing, all text is readable |

## v0.7 verified (Unity 6000.6.3f1 editor, Play mode driven by script)

| Item | Result |
|---|---|
| Enemy approaching from 4 units away: when it starts winding up | Edge distance 0.30 (= triggerRange) |
| Player stands still | HP 50 → 45 (small enemy damage 5) |
| Player walks 1.8 units away 0.2 s into the wind-up | HP 50 → 50, missed |
| Gun tap, enemy 4.5 units away | Enemy HP 10/10, out of reach |
| While charging the gun | Move speed multiplier 0.4; charge 1.00 after 1.2 s |
| Full charge swing, enemy 4.5 units away | Enemy dies (damage 15) |
| Throwing knife | 7.9 units away after 0.4 s (independent projectile) |
| Hook | Hooks an enemy 6 units away, it enters Hooked, the player holds cover |
| Illustrated switch | Floor changes to the sand texture; wave 1 spawns normally and all enemies are billboards; no errors in the Console |

**Not verified**: the WebGL build and its behavior in a browser; menu 1 (full scene rebuild) was not rerun on the v0.7 code, only menu 3.

## Where the spec was open, and what is done for now

| Item | Current behavior |
|---|---|
| Player move speed | 6 units/s (small enemy 5.4, large 3.6) |
| Missile random direction | Fully random over 360°; `Random Spread Degrees` in `MissileHoming.asset` can limit it to an angle either side of the aim direction |
| Clearing projectiles | Only on player death or victory; not on wave change (per the new requirement) |
| Reverse Shot recoil into a wall | Counts as a push; no wall damage |
| Hook throwing a large enemy | Also 8 units (the body-size factor only affects knockback) |
| Being hit during protection | Damage and knockback are both ignored |
| Enemies walking on their own | Never walk into a gap; they only fall when knocked back / thrown |
| Game start | Click START or press Space on the title screen (in a browser, click once first for focus); clicking elsewhere does not start the game, so clicking the art toggle does not start it by accident; restarting with R skips the title and starts from wave 1 |
| UI language | English, Unity's built-in font |
| Illustrated mode | Switched on the title screen; whitebox by default |
| Supply | 3 / 4 / 5 weapons at the start of each wave (cap 6) + 50% large-enemy drop; no timed supply |
| Arena | Irregular 13-sided polygon (`ArenaShape`), 3 large gaps + 2 small 1.6-unit gaps |
| Melee | Punch, Charge Swing and enemy punch each hit a single target |
