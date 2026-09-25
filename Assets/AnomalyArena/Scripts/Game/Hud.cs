using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// 文字界面（IMGUI，Unity 自带字体）：左上血条 / 波次 / 剩余敌人，敌人头顶血条，左下手上的武器，
    /// 地上武器名字，换武器提示，结束画面。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        GUIStyle style;
        float u;
        string toast;
        Color toastColor;
        float toastTimer;
        string bannerTitle, bannerSub;
        float bannerTimer;
        float damageFlash;

        static GameManager GM => GameManager.Instance;

        public void Toast(string text, Color c)
        {
            toast = text;
            toastColor = c;
            toastTimer = 2.5f;
        }

        public void Banner(string title, string sub)
        {
            bannerTitle = title;
            bannerSub = sub;
            bannerTimer = 2f;
        }

        public void FlashDamage() => damageFlash = 1f;

        void Update()
        {
            float dt = Time.deltaTime;
            toastTimer -= dt;
            bannerTimer -= dt;
            damageFlash = Mathf.Max(0f, damageFlash - dt * 3f);
        }

        void OnGUI()
        {
            if (GM == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { wordWrap = false };
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
            if (damageFlash > 0f) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0f, 0f, 0.18f * damageFlash));
        }

        // ───── 场景内 ─────

        void WorldLabels()
        {
            var cam = GM.cam;
            if (cam == null) return;
            foreach (var w in GM.weapons.ground)
            {
                if (!w || !ToScreen(cam, w.transform.position + Vector3.up * 1.4f, out var sp)) continue;
                var size = Measure(w.Label, 15);
                var r = new Rect(sp.x - size.x * 0.5f - 6 * u, sp.y - 12 * u, size.x + 12 * u, 24 * u);
                Box(r, new Color(0f, 0f, 0f, 0.55f));
                Text(r, w.Label, 15, w.Revealed ? new Color(1f, 0.85f, 0.4f) : Color.white, TextAnchor.MiddleCenter);
            }
            foreach (var e in GM.waves.Alive)
            {
                if (!e || !e.IsAlive) continue;
                if (!ToScreen(cam, e.Position + Vector3.up * (e.radius * 2f + 0.6f), out var sp)) continue;
                float w = (e.IsLarge ? 70 : 36) * u, h = 5 * u;
                var r = new Rect(sp.x - w * 0.5f, sp.y, w, h);
                Box(r, new Color(0f, 0f, 0f, 0.6f));
                Box(new Rect(r.x, r.y, w * Mathf.Clamp01(e.Hp / e.maxHp), h), new Color(1f, 0.3f, 0.25f));
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
            var panel = new Rect(16 * u, 16 * u, 360 * u, 92 * u);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            float x = panel.x + 12 * u, y = panel.y + 10 * u, w = panel.width - 24 * u;

            // 血条：50 格
            var bar = new Rect(x, y, w, 18 * u);
            Box(bar, new Color(1f, 1f, 1f, 0.12f));
            float k = Mathf.Clamp01(p.Hp / p.maxHp);
            Box(new Rect(bar.x, bar.y, bar.width * k, bar.height), Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.8f, 0.45f), k));
            Text(bar, $"HP {Mathf.CeilToInt(p.Hp)} / {Mathf.RoundToInt(p.maxHp)}", 14, Color.white, TextAnchor.MiddleCenter);

            var wv = GM.waves;
            Text(new Rect(x, y + 28 * u, w, 24 * u), $"Wave {Mathf.Max(1, wv.WaveIndex + 1)} / {wv.WaveCount}", 18, Color.white, TextAnchor.MiddleLeft);
            string right = wv.BetweenWaves ? $"Next wave in {Mathf.CeilToInt(wv.BetweenTimer)}s" : $"Enemies left {wv.Remaining}";
            Text(new Rect(x, y + 28 * u, w, 24 * u), right, 18, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleRight);
            Text(new Rect(x, y + 54 * u, w, 20 * u), $"Time {GM.Elapsed:0}s", 13, new Color(0.75f, 0.78f, 0.82f), TextAnchor.MiddleLeft);
        }

        /// <summary>左下：手上的武器，比如 “Knife ? 4/4” 或 “Knife · Hook 2/4”。</summary>
        void DrawWeapon()
        {
            var p = GM.player;
            var w = p.Weapon;
            var panel = new Rect(16 * u, Screen.height - 86 * u, 460 * u, 70 * u);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            var l1 = new Rect(panel.x + 12 * u, panel.y + 8 * u, panel.width - 24 * u, 28 * u);
            var l2 = new Rect(l1.x, l1.y + 30 * u, l1.width, 22 * u);
            if (w == null)
            {
                Text(l1, "No weapon", 20, Color.white, TextAnchor.MiddleLeft);
                Text(l2, "Unarmed: you can't attack. Dodge and grab a weapon.", 13, new Color(0.75f, 0.78f, 0.82f), TextAnchor.MiddleLeft);
            }
            else
            {
                Text(l1, $"{w.Label}  {w.UsesLeft}/{w.MaxUses}", 20, w.Revealed ? new Color(1f, 0.85f, 0.4f) : Color.white, TextAnchor.MiddleLeft);
                string desc;
                if (p.HeldShield != null) desc = "Holding an enemy: left-click to throw it";
                else if (!w.Revealed) desc = "Unknown effect. Use it once to find out.";
                else desc = w.Effect.description;
                Text(l2, desc, 13, new Color(0.75f, 0.78f, 0.82f), TextAnchor.MiddleLeft);
            }

            if (w != null && p.NearPickup != null && GM.State == GameState.Playing)
            {
                string msg = $"Right-click to swap for {p.NearPickup.Label}";
                var size = Measure(msg, 16);
                var r = new Rect(panel.x, panel.y - 40 * u, size.x + 24 * u, 32 * u);
                Box(r, new Color(0.85f, 0.65f, 0.15f, 0.92f));
                Text(r, msg, 16, new Color(0.1f, 0.08f, 0.02f), TextAnchor.MiddleCenter);
            }
        }

        void DrawCenter()
        {
            float y = Screen.height * 0.12f;
            if (bannerTimer > 0f)
            {
                float a = Mathf.Clamp01(bannerTimer / 0.4f);
                Text(new Rect(0, y, Screen.width, 50 * u), bannerTitle, 38, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                Text(new Rect(0, y + 48 * u, Screen.width, 26 * u), bannerSub, 17, new Color(0.85f, 0.88f, 0.92f, a), TextAnchor.MiddleCenter);
                y += 84 * u;
            }
            if (toastTimer > 0f)
            {
                var c = toastColor;
                c.a = Mathf.Clamp01(toastTimer / 0.4f);
                Text(new Rect(0, y, Screen.width, 30 * u), toast, 20, c, TextAnchor.MiddleCenter);
            }
        }

        void DrawTitle()
        {
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.06f, 0.7f));
            float cy = Screen.height * 0.24f;
            Text(new Rect(0, cy, Screen.width, 70 * u), "ANOMALY WEAPON ARENA", 48, Color.white, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 70 * u, Screen.width, 28 * u),
                "Every weapon looks familiar. What it actually does, you find out the first time you use it.", 17,
                new Color(0.78f, 0.8f, 0.85f), TextAnchor.MiddleCenter);
            string[] lines =
            {
                "WASD  move        Mouse  aim        Left-click  use weapon / throw",
                "Walk over a weapon with empty hands to pick it up",
                "Holding a weapon? Stand on another one and right-click to swap",
                "Every effect can hurt you too. Lose all HP or fall into a gap and you lose.",
            };
            float y = cy + 130 * u;
            foreach (var l in lines)
            {
                Text(new Rect(0, y, Screen.width, 28 * u), l, 16, Color.white, TextAnchor.MiddleCenter);
                y += 32 * u;
            }
            float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
            Text(new Rect(0, y + 30 * u, Screen.width, 36 * u), "Click or press Space to start", 24, new Color(1f, 0.85f, 0.4f, blink), TextAnchor.MiddleCenter);
        }

        void DrawEnd()
        {
            bool won = GM.State == GameState.Won;
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.55f));
            string title = won ? "VICTORY"
                : GM.LoseCause == DeathCause.FellIntoGap ? "DEFEAT · Fell into a gap" : "DEFEAT · HP depleted";
            float cy = Screen.height * 0.38f;
            Text(new Rect(0, cy, Screen.width, 64 * u), title, 46, won ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 70 * u, Screen.width, 32 * u), "Press R to restart", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
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

        // ───── 工具 ─────

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
