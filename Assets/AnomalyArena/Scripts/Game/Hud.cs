using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// 英文文字界面（IMGUI，Unity 自带字体）：
    /// 左上生命 / 波次 / 剩余敌人，底部当前武器与次数（蓄力时显示蓄力条），地上武器名字，敌人头顶血条，换装提示，
    /// 揭晓横幅，结束画面；标题画面有 START 和白盒 / 美术版切换按钮。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static readonly Color TextGood = new Color(0.55f, 1f, 0.6f);
        static readonly Color TextBad = new Color(1f, 0.5f, 0.45f);
        static readonly Color TextWeapon = new Color(1f, 0.85f, 0.4f);
        static readonly Color TextDim = new Color(0.75f, 0.78f, 0.82f);

        class Msg
        {
            public string text;
            public Color color;
            public float t, life;
        }

        /// <summary>飘字：受击处冒出的伤害数字，往上飘、刚出现时放大一下再缩回。</summary>
        class DamageNum
        {
            public Vector3 world;
            public string text;
            public Color color;
            public int size;
            public float t;
        }

        // 飘字存活秒数（真实时间，顿帧时也照常飘）
        const float DamageNumLife = 0.8f;

        GUIStyle style;
        float u;
        readonly List<Msg> msgs = new List<Msg>();
        readonly List<DamageNum> nums = new List<DamageNum>();
        string revealTitle, revealDesc;
        float revealT;
        string bannerTitle, bannerSub;
        float bannerT;
        float damageFlash;

        static GameManager GM => GameManager.Instance;

        /// <summary>中间的一行提示，最多同时 4 条。</summary>
        public void Toast(string text, Color c, float life = 2.2f)
        {
            msgs.Add(new Msg { text = text, color = c, life = life });
            if (msgs.Count > 4) msgs.RemoveAt(0);
        }

        public void Reveal(Weapon w)
        {
            revealTitle = $"Revealed: this {Weapon.TypeName(w.type)} is \"{w.Effect.displayName}\"";
            revealDesc = w.Effect.description;
            revealT = 3.5f;
        }

        public void Banner(string title, string sub)
        {
            bannerTitle = title;
            bannerSub = sub;
            bannerT = 2.2f;
        }

        public void FlashDamage() => damageFlash = 1f;

        /// <summary>在 world 处冒一个伤害数字；heavy 时字更大。</summary>
        public void DamageNumber(Vector3 world, float amount, Color c, bool heavy)
        {
            // 同一位置附近随机错开一点，连续几下不会叠成一个
            Vector2 jitter = Random.insideUnitCircle * 0.4f;
            nums.Add(new DamageNum
            {
                world = world + new Vector3(jitter.x, 0f, jitter.y),
                text = $"-{Mathf.RoundToInt(amount)}",
                color = c,
                size = heavy ? 34 : 24,
            });
        }

        void Update()
        {
            for (int i = nums.Count - 1; i >= 0; i--)
            {
                nums[i].t += Time.unscaledDeltaTime;
                if (nums[i].t >= DamageNumLife) nums.RemoveAt(i);
            }

            float dt = Time.deltaTime;
            revealT -= dt;
            bannerT -= dt;
            damageFlash = Mathf.Max(0f, damageFlash - dt * 3f);
            for (int i = msgs.Count - 1; i >= 0; i--)
            {
                msgs[i].t += dt;
                if (msgs[i].t >= msgs[i].life) msgs.RemoveAt(i);
            }
        }

        void OnGUI()
        {
            if (GM == null) return;
            if (style == null)
                style = new GUIStyle
                    { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), wordWrap = false };
            u = Screen.height / 900f;

            WorldLabels();
            if (GM.State == GameState.Title)
            {
                DrawTitle();
                return;
            }

            DrawStatus();
            DrawWeapon();
            DrawCenter();
            if (GM.State == GameState.Won || GM.State == GameState.Lost) DrawEnd();
            else DrawReticle();
            if (damageFlash > 0f)
                Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0f, 0f, 0.15f * damageFlash));
        }

        // ───── 场景内标签 ─────

        void WorldLabels()
        {
            var cam = GM.cam;
            if (cam == null) return;
            foreach (var w in GM.weapons.ground)
            {
                // 标签放在武器贴图上方（底边对齐锚点），深色底板，不压住武器
                if (!w || !ToScreen(cam, w.LabelPoint(cam), out var sp)) continue;
                string s = w.Revealed ? $"{w.Label} x{w.UsesLeft}" : w.Label;
                var size = Measure(s, 15);
                var r = new Rect(sp.x - size.x * 0.5f - 7 * u, sp.y - 26 * u, size.x + 14 * u, 24 * u);
                Box(r, new Color(0.03f, 0.04f, 0.06f, 0.85f));
                Text(r, s, 15, w.Revealed ? TextWeapon : Color.white, TextAnchor.MiddleCenter);
            }

            foreach (var e in GM.waves.Alive)
            {
                if (!e || !e.IsAlive) continue;
                if (!ToScreen(cam, e.HeadPoint(cam), out var sp)) continue;
                float wd = (e.IsLarge ? 70 : 38) * u, h = 6 * u;
                var r = new Rect(sp.x - wd * 0.5f, sp.y, wd, h);
                Box(r, new Color(0f, 0f, 0f, 0.6f));
                Box(new Rect(r.x, r.y, wd * Mathf.Clamp01(e.Hp / e.maxHp), h), new Color(1f, 0.35f, 0.3f));
            }

            foreach (var n in nums)
            {
                float k = n.t / DamageNumLife;
                if (!ToScreen(cam, n.world + cam.transform.up * (0.6f + k * 1.6f), out var sp)) continue;
                float pop = 1f + 0.6f * Mathf.Clamp01(1f - n.t / 0.12f); // 刚出现时大 60%，0.12 秒缩回
                var c = n.color;
                c.a = 1f - k * k;
                int size = Mathf.RoundToInt(n.size * pop);
                Text(new Rect(sp.x - 100 * u, sp.y - 30 * u, 200 * u, 60 * u), n.text, size, c,
                    TextAnchor.MiddleCenter);
            }
        }

        static bool ToScreen(Camera cam, Vector3 world, out Vector2 gui)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            gui = new Vector2(s.x, Screen.height - s.y);
            return s.z > 0f;
        }

        // ───── 面板 ─────

        void DrawStatus()
        {
            var p = GM.player;
            var wv = GM.waves;
            var panel = new Rect(18 * u, 18 * u, 330 * u, 146 * u);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            float x = panel.x + 14 * u, y = panel.y + 10 * u, w = panel.width - 28 * u;

            Text(new Rect(x, y, w, 24 * u), $"HP  {Mathf.CeilToInt(p.Hp)} / {Mathf.RoundToInt(p.maxHp)}", 17,
                Color.white, TextAnchor.MiddleLeft);
            var bar = new Rect(x, y + 28 * u, w, 12 * u);
            Box(bar, new Color(1f, 1f, 1f, 0.12f));
            float k = Mathf.Clamp01(p.Hp / p.maxHp);
            Box(new Rect(bar.x, bar.y, bar.width * k, bar.height),
                Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.8f, 0.45f), k));

            Text(new Rect(x, y + 50 * u, w, 26 * u), $"Wave {Mathf.Max(1, wv.WaveIndex + 1)} / {wv.WaveCount}", 17,
                Color.white, TextAnchor.MiddleLeft);
            string right = wv.BetweenWaves
                ? $"Next wave in {Mathf.CeilToInt(wv.BetweenTimer)}s"
                : $"Enemies left: {wv.Remaining}";
            Text(new Rect(x, y + 50 * u, w, 26 * u), right, 17, TextBad, TextAnchor.MiddleRight);
            Text(new Rect(x, y + 78 * u, w, 22 * u), $"Kills {wv.Kills}    Time {GM.Elapsed:0}s", 14, TextDim,
                TextAnchor.MiddleLeft);

            // 体力条：冲刺中亮黄、耗尽后变灰直到回到可以再冲的量
            var st = p.stamina;
            Text(new Rect(x, y + 102 * u, 90 * u, 20 * u), "Stamina", 13, TextDim, TextAnchor.MiddleLeft);
            var sBar = new Rect(x + 70 * u, y + 107 * u, w - 70 * u, 10 * u);
            Box(sBar, new Color(1f, 1f, 1f, 0.12f));
            Color sc = st.Exhausted ? new Color(0.5f, 0.5f, 0.5f) :
                st.Sprinting ? new Color(1f, 0.9f, 0.3f) : new Color(0.95f, 0.75f, 0.25f);
            Box(new Rect(sBar.x, sBar.y, sBar.width * st.Value, sBar.height), sc);
        }

        void DrawWeapon()
        {
            var p = GM.player;
            var w = p.Weapon;
            float pw = 600 * u, ph = 86 * u;
            var panel = new Rect((Screen.width - pw) * 0.5f, Screen.height - ph - 18 * u, pw, ph);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            var inner = new Rect(panel.x + 16 * u, panel.y + 10 * u, panel.width - 32 * u, 30 * u);
            var line2 = new Rect(inner.x, inner.y + 36 * u, inner.width, 28 * u);

            if (w == null)
            {
                Text(inner, "Bare hands", 20, Color.white, TextAnchor.MiddleLeft);
                Text(line2, "Left click to punch: 3 damage, knocks enemies back. Walk over a weapon to pick it up.", 14,
                    TextDim, TextAnchor.MiddleLeft);
            }
            else
            {
                Text(inner, w.Label, 20, w.Revealed ? TextWeapon : Color.white, TextAnchor.MiddleLeft);
                // 有弹夹的武器（反向射击）同时显示弹夹余量
                string uses = w.RoundsLeft > 0
                    ? $"Uses {w.UsesLeft}/{w.MaxUses}  +{w.RoundsLeft} rounds"
                    : $"Uses {w.UsesLeft}/{w.MaxUses}";
                Text(inner, uses, 20, w.UsesLeft > 0 || w.RoundsLeft > 0 ? Color.white : TextBad,
                    TextAnchor.MiddleRight);
                if (w.Active is SwingCharge charge)
                {
                    // 蓄力条：满了闪烁
                    float c = charge.Charge01;
                    var bar = new Rect(line2.x, line2.y + 8 * u, line2.width * 0.55f, 12 * u);
                    Box(bar, new Color(1f, 1f, 1f, 0.12f));
                    float blink = c >= 1f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 25f) : 1f;
                    Box(new Rect(bar.x, bar.y, bar.width * c, bar.height), new Color(1f, 0.75f, 0.2f, blink));
                    Text(new Rect(bar.xMax + 12 * u, line2.y, line2.width * 0.45f - 12 * u, line2.height),
                        c >= 1f ? "FULL - release to swing!" : "Charging... release to swing", 14, TextWeapon,
                        TextAnchor.MiddleLeft);
                }
                else
                {
                    string desc;
                    if (p.HeldShield != null) desc = "Enemy hooked: left click to throw it where you aim (try a gap!)";
                    else if (w.InFlight) desc = "Knife in flight - it has to come back before you can throw it again";
                    else if (!w.Revealed) desc = "Unknown effect - use it (left click) to find out";
                    else desc = w.Effect.description;
                    Text(line2, desc, 14, p.HeldShield != null ? TextGood : TextDim, TextAnchor.MiddleLeft);
                }
            }

            // 已装备时站在武器上，提示右键才会换装
            if (w != null && p.NearPickup != null && GM.State == GameState.Playing)
            {
                var hint = new Rect((Screen.width - 520 * u) * 0.5f, panel.y - 42 * u, 520 * u, 34 * u);
                Box(hint, new Color(0.85f, 0.65f, 0.15f, 0.9f));
                Text(hint, $"Right click to swap for {p.NearPickup.Label} (current weapon stays here)", 16,
                    new Color(0.1f, 0.08f, 0.02f), TextAnchor.MiddleCenter);
            }
        }

        void DrawCenter()
        {
            float y = Screen.height * 0.13f;
            if (bannerT > 0f)
            {
                float a = Mathf.Clamp01(bannerT / 0.4f);
                // 深色横条垫底：在花哨的地面上也看得清
                Box(new Rect(0, y - 6 * u, Screen.width, 92 * u), new Color(0.03f, 0.04f, 0.06f, 0.6f * a));
                Text(new Rect(0, y, Screen.width, 56 * u), bannerTitle, 40, new Color(1f, 1f, 1f, a),
                    TextAnchor.MiddleCenter);
                Text(new Rect(0, y + 54 * u, Screen.width, 28 * u), bannerSub, 17, new Color(0.85f, 0.88f, 0.92f, a),
                    TextAnchor.MiddleCenter);
                y += 96 * u;
            }

            if (revealT > 0f)
            {
                float a = Mathf.Clamp01(revealT / 0.5f);
                var r = new Rect((Screen.width - 760 * u) * 0.5f, y, 760 * u, 76 * u);
                Box(r, new Color(0.08f, 0.06f, 0.02f, 0.8f * a));
                Text(new Rect(r.x, r.y + 6 * u, r.width, 34 * u), revealTitle, 24, new Color(1f, 0.85f, 0.4f, a),
                    TextAnchor.MiddleCenter);
                Text(new Rect(r.x + 12 * u, r.y + 40 * u, r.width - 24 * u, 28 * u), revealDesc, 15,
                    new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                y += 86 * u;
            }

            foreach (var m in msgs)
            {
                var c = m.color;
                c.a = Mathf.Clamp01((m.life - m.t) / 0.4f);
                float tw = Measure(m.text, 18).x + 24 * u;
                Box(new Rect((Screen.width - tw) * 0.5f, y + 1 * u, tw, 26 * u),
                    new Color(0.03f, 0.04f, 0.06f, 0.7f * c.a));
                Text(new Rect(0, y, Screen.width, 28 * u), m.text, 18, c, TextAnchor.MiddleCenter);
                y += 28 * u;
            }
        }

        void DrawTitle()
        {
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.06f, 0.72f));
            float cy = Screen.height * 0.16f;
            Text(new Rect(0, cy, Screen.width, 80 * u), "ANOMALY WEAPON ARENA", 56, Color.white,
                TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 84 * u, Screen.width, 30 * u),
                "Every weapon looks familiar - you only learn what it really does when you use it. Clear 3 waves, or knock them into the gaps.",
                17, TextDim, TextAnchor.MiddleCenter);

            string[] lines =
            {
                "WASD  move      Shift  sprint (uses stamina)      Mouse  aim      Left click  attack / throw",
                "Walk over a weapon to pick it up; while armed, stand on one and right click to swap",
                "Weapons have limited uses; once revealed, a weapon's effect never changes",
                "Every effect can hurt you too. You lose if your HP hits 0 or you fall into a gap",
                "Enemies flash yellow right before they punch - step away to dodge",
            };
            float y = cy + 150 * u;
            var panel = new Rect((Screen.width - 780 * u) * 0.5f, y - 14 * u, 780 * u, lines.Length * 34 * u + 28 * u);
            Box(panel, new Color(1f, 1f, 1f, 0.06f));
            foreach (var l in lines)
            {
                Text(new Rect(0, y, Screen.width, 30 * u), l, 16, Color.white, TextAnchor.MiddleCenter);
                y += 34 * u;
            }

            // 两个按钮：开局、切换白盒 / 美术版（切换会重新加载场景）
            y += 36 * u;
            float bw = 260 * u, bh = 54 * u, gap = 24 * u;
            var start = new Rect(Screen.width * 0.5f - bw - gap * 0.5f, y, bw, bh);
            var art = new Rect(Screen.width * 0.5f + gap * 0.5f, y, bw, bh);
            float blink = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
            if (Button(start, "START", new Color(0.85f, 0.65f, 0.15f, blink), new Color(0.1f, 0.08f, 0.02f)))
                GM.BeginPlaying();
            string artLabel = Art.Enabled ? "ART: ILLUSTRATED" : "ART: WHITEBOX";
            if (Button(art, artLabel, new Color(0.25f, 0.45f, 0.8f, 0.95f), Color.white)) GM.ToggleArt();
            Text(new Rect(0, y + bh + 10 * u, Screen.width, 26 * u),
                "Space = start      T = switch art (whitebox <-> illustrated)", 15, TextDim, TextAnchor.MiddleCenter);
        }

        void DrawEnd()
        {
            bool won = GM.State == GameState.Won;
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.55f));
            float cy = Screen.height * 0.34f;
            string title = won ? "VICTORY" :
                GM.LoseCause == DeathCause.FellIntoGap ? "DEFEAT - Fell into a gap" : "DEFEAT - Out of HP";
            Text(new Rect(0, cy, Screen.width, 70 * u), title, 54, won ? TextGood : TextBad, TextAnchor.MiddleCenter);
            string sub = won ? "All three waves cleared" : $"Reached wave {Mathf.Max(1, GM.waves.WaveIndex + 1)}";
            Text(new Rect(0, cy + 72 * u, Screen.width, 32 * u), sub, 20, Color.white, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 108 * u, Screen.width, 28 * u), $"Kills {GM.waves.Kills} · Time {GM.Elapsed:0}s", 16,
                TextDim, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 160 * u, Screen.width, 34 * u), "Press R to restart", 22, new Color(1f, 0.85f, 0.4f),
                TextAnchor.MiddleCenter);
        }

        void DrawReticle()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 m = mouse.position.ReadValue();
            float x = m.x, y = Screen.height - m.y, s = 9 * u, t = 2 * u;
            var c = new Color(1f, 1f, 1f, 0.9f);
            Box(new Rect(x - s, y - t * 0.5f, s * 0.6f, t), c);
            Box(new Rect(x + s * 0.4f, y - t * 0.5f, s * 0.6f, t), c);
            Box(new Rect(x - t * 0.5f, y - s, t, s * 0.6f), c);
            Box(new Rect(x - t * 0.5f, y + s * 0.4f, t, s * 0.6f), c);
        }

        // ───── 绘制工具 ─────

        bool Button(Rect r, string label, Color bg, Color fg)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Box(r, hover ? Color.Lerp(bg, Color.white, 0.2f) : bg);
            Text(r, label, 22, fg, TextAnchor.MiddleCenter);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        Vector2 Measure(string s, int size)
        {
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * u));
            return style.CalcSize(new GUIContent(s));
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor anchor)
        {
            if (string.IsNullOrEmpty(s)) return;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * u));
            style.alignment = anchor;
            var shadow = new Rect(r.x + 1.5f * u, r.y + 1.5f * u, r.width, r.height);
            style.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.7f);
            GUI.Label(shadow, s, style);
            style.normal.textColor = c;
            GUI.Label(r, s, style);
        }
    }
}