using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// 中文文字界面（IMGUI，沿用上个版本的面板样式和思源黑体子集字体）：
    /// 左上生命 / 波次 / 剩余敌人，底部当前武器与次数，地上武器名字，敌人头顶血条，换装提示，揭晓横幅，结束画面。
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

        GUIStyle style;
        float u;
        readonly List<Msg> msgs = new List<Msg>();
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
            revealTitle = $"揭晓：这把{Weapon.TypeName(w.type)}是「{w.Effect.displayName}」";
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

        void Update()
        {
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
            if (style == null) style = new GUIStyle { font = GM.uiFont, wordWrap = false };
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
            if (damageFlash > 0f) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0f, 0f, 0.15f * damageFlash));
        }

        // ───── 场景内标签 ─────

        void WorldLabels()
        {
            var cam = GM.cam;
            if (cam == null) return;
            foreach (var w in GM.weapons.ground)
            {
                if (!w || !ToScreen(cam, w.transform.position + Vector3.up * 1.4f, out var sp)) continue;
                string s = w.Revealed ? $"{w.Label} ×{w.UsesLeft}" : w.Label;
                var size = Measure(s, 15);
                var r = new Rect(sp.x - size.x * 0.5f - 7 * u, sp.y - 13 * u, size.x + 14 * u, 26 * u);
                Box(r, new Color(0f, 0f, 0f, 0.55f));
                Text(r, s, 15, w.Revealed ? TextWeapon : Color.white, TextAnchor.MiddleCenter);
            }
            foreach (var e in GM.waves.Alive)
            {
                if (!e || !e.IsAlive) continue;
                if (!ToScreen(cam, e.Position + Vector3.up * (e.radius * 2f + 0.6f), out var sp)) continue;
                float wd = (e.IsLarge ? 70 : 38) * u, h = 6 * u;
                var r = new Rect(sp.x - wd * 0.5f, sp.y, wd, h);
                Box(r, new Color(0f, 0f, 0f, 0.6f));
                Box(new Rect(r.x, r.y, wd * Mathf.Clamp01(e.Hp / e.maxHp), h), new Color(1f, 0.35f, 0.3f));
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
            var panel = new Rect(18 * u, 18 * u, 330 * u, 118 * u);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            float x = panel.x + 14 * u, y = panel.y + 10 * u, w = panel.width - 28 * u;

            Text(new Rect(x, y, w, 24 * u), $"生命  {Mathf.CeilToInt(p.Hp)} / {Mathf.RoundToInt(p.maxHp)}", 17, Color.white, TextAnchor.MiddleLeft);
            var bar = new Rect(x, y + 28 * u, w, 12 * u);
            Box(bar, new Color(1f, 1f, 1f, 0.12f));
            float k = Mathf.Clamp01(p.Hp / p.maxHp);
            Box(new Rect(bar.x, bar.y, bar.width * k, bar.height), Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.8f, 0.45f), k));

            Text(new Rect(x, y + 50 * u, w, 26 * u), $"第 {Mathf.Max(1, wv.WaveIndex + 1)} / {wv.WaveCount} 波", 17, Color.white, TextAnchor.MiddleLeft);
            string right = wv.BetweenWaves ? $"下一波 {Mathf.CeilToInt(wv.BetweenTimer)} 秒" : $"本波剩余 {wv.Remaining}";
            Text(new Rect(x, y + 50 * u, w, 26 * u), right, 17, TextBad, TextAnchor.MiddleRight);
            Text(new Rect(x, y + 78 * u, w, 22 * u), $"击杀 {wv.Kills}    用时 {GM.Elapsed:0}s", 14, TextDim, TextAnchor.MiddleLeft);
        }

        void DrawWeapon()
        {
            var p = GM.player;
            var w = p.Weapon;
            float pw = 560 * u, ph = 86 * u;
            var panel = new Rect((Screen.width - pw) * 0.5f, Screen.height - ph - 18 * u, pw, ph);
            Box(panel, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            var inner = new Rect(panel.x + 16 * u, panel.y + 10 * u, panel.width - 32 * u, 30 * u);
            var line2 = new Rect(inner.x, inner.y + 36 * u, inner.width, 28 * u);

            if (w == null)
            {
                Text(inner, "徒手", 20, Color.white, TextAnchor.MiddleLeft);
                Text(line2, "左键出拳：伤害 3，能把敌人打退。走到武器上自动拾取。", 14, TextDim, TextAnchor.MiddleLeft);
            }
            else
            {
                Text(inner, w.Label, 20, w.Revealed ? TextWeapon : Color.white, TextAnchor.MiddleLeft);
                Text(inner, $"剩余 {w.UsesLeft}/{w.MaxUses}", 20, w.UsesLeft > 0 ? Color.white : TextBad, TextAnchor.MiddleRight);
                string desc;
                if (p.HeldShield != null) desc = "已钩住敌人：左键朝瞄准方向扔出去（可以扔进缺口）";
                else if (!w.Revealed) desc = "未知效果——左键使用后揭晓";
                else desc = w.Effect.description;
                Text(line2, desc, 14, p.HeldShield != null ? TextGood : TextDim, TextAnchor.MiddleLeft);
            }

            // 已装备时站在武器上，提示右键才会换装
            if (w != null && p.NearPickup != null && GM.State == GameState.Playing)
            {
                var hint = new Rect((Screen.width - 460 * u) * 0.5f, panel.y - 42 * u, 460 * u, 34 * u);
                Box(hint, new Color(0.85f, 0.65f, 0.15f, 0.9f));
                Text(hint, $"右键换装：{p.NearPickup.Label}（当前武器留在地上）", 16, new Color(0.1f, 0.08f, 0.02f), TextAnchor.MiddleCenter);
            }
        }

        void DrawCenter()
        {
            float y = Screen.height * 0.13f;
            if (bannerT > 0f)
            {
                float a = Mathf.Clamp01(bannerT / 0.4f);
                Text(new Rect(0, y, Screen.width, 56 * u), bannerTitle, 40, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                Text(new Rect(0, y + 54 * u, Screen.width, 28 * u), bannerSub, 17, new Color(0.85f, 0.88f, 0.92f, a), TextAnchor.MiddleCenter);
                y += 96 * u;
            }
            if (revealT > 0f)
            {
                float a = Mathf.Clamp01(revealT / 0.5f);
                var r = new Rect((Screen.width - 720 * u) * 0.5f, y, 720 * u, 76 * u);
                Box(r, new Color(0.08f, 0.06f, 0.02f, 0.8f * a));
                Text(new Rect(r.x, r.y + 6 * u, r.width, 34 * u), revealTitle, 24, new Color(1f, 0.85f, 0.4f, a), TextAnchor.MiddleCenter);
                Text(new Rect(r.x + 12 * u, r.y + 40 * u, r.width - 24 * u, 28 * u), revealDesc, 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                y += 86 * u;
            }
            foreach (var m in msgs)
            {
                var c = m.color;
                c.a = Mathf.Clamp01((m.life - m.t) / 0.4f);
                Text(new Rect(0, y, Screen.width, 28 * u), m.text, 18, c, TextAnchor.MiddleCenter);
                y += 28 * u;
            }
        }

        void DrawTitle()
        {
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.04f, 0.06f, 0.72f));
            float cy = Screen.height * 0.2f;
            Text(new Rect(0, cy, Screen.width, 80 * u), "反常武器竞技场", 56, Color.white, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 84 * u, Screen.width, 30 * u), "武器的外形很熟悉，功能要第一次用了才知道。清空三波敌人，或者把它们打进缺口。", 18, TextDim, TextAnchor.MiddleCenter);

            string[] lines =
            {
                "W A S D  移动　　鼠标  瞄准　　左键  攻击 / 投掷（空手时出拳）",
                "空手走到武器上自动拾取　　持有武器时站在武器上按右键换装",
                "每把武器次数有限；同一把武器的效果揭晓后固定不变",
                "每种效果都可能伤到你自己；生命耗尽，或掉进缺口，都算失败",
            };
            float y = cy + 150 * u;
            var panel = new Rect((Screen.width - 680 * u) * 0.5f, y - 14 * u, 680 * u, lines.Length * 34 * u + 28 * u);
            Box(panel, new Color(1f, 1f, 1f, 0.06f));
            foreach (var l in lines)
            {
                Text(new Rect(0, y, Screen.width, 30 * u), l, 17, Color.white, TextAnchor.MiddleCenter);
                y += 34 * u;
            }
            float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
            Text(new Rect(0, y + 40 * u, Screen.width, 40 * u), "点击左键 或 按空格 开始", 24, new Color(1f, 0.85f, 0.4f, blink), TextAnchor.MiddleCenter);
        }

        void DrawEnd()
        {
            bool won = GM.State == GameState.Won;
            Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.55f));
            float cy = Screen.height * 0.34f;
            string title = won ? "胜利" : GM.LoseCause == DeathCause.FellIntoGap ? "失败 · 掉进缺口" : "失败 · 生命归零";
            Text(new Rect(0, cy, Screen.width, 70 * u), title, 54, won ? TextGood : TextBad, TextAnchor.MiddleCenter);
            string sub = won ? "三波敌人全部清除" : $"到达第 {Mathf.Max(1, GM.waves.WaveIndex + 1)} 波";
            Text(new Rect(0, cy + 72 * u, Screen.width, 32 * u), sub, 20, Color.white, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 108 * u, Screen.width, 28 * u), $"击杀 {GM.waves.Kills} · 用时 {GM.Elapsed:0} 秒", 16, TextDim, TextAnchor.MiddleCenter);
            Text(new Rect(0, cy + 160 * u, Screen.width, 34 * u), "按 R 重开", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
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
