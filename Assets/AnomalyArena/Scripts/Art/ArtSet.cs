using UnityEngine;

namespace AnomalyArena
{
    /// <summary>美术版用到的贴图。只在标题画面切到美术版后才会用到；白盒版完全不读。</summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Art Set")]
    public class ArtSet : ScriptableObject
    {
        [Header("Characters")]
        public Texture2D player;
        public Texture2D enemySmall;
        public Texture2D enemyLarge;

        [Header("Weapons (ground / held)")]
        public Texture2D gun;
        public Texture2D knife;
        public Texture2D missileLauncher;

        [Header("Knife hook: hilt + stretched middle + tip")]
        public Texture2D hookHilt;
        public Texture2D hookMid;
        public Texture2D hookTip;

        [Header("Projectiles")]
        public Texture2D missile;
        [Tooltip("导弹尾焰；发射自己时也拖在玩家身后")] public Texture2D flame;

        [Header("Arena")]
        public Texture2D floor;
        [Tooltip("每块地板贴图覆盖几格；越大花纹越少")] public float floorTileSize = 5f;
        [Tooltip("缺口边缘：上半沙地、下半黑坑")] public Texture2D gapEdge;

        [Header("Sizes (world units)")]
        // 角色立牌宽度大致和碰撞体直径对齐（玩家 / 小型 1 格，大型 2 格 = 小型的两倍），武器比角色显眼
        public float playerHeight = 1.4f;
        public float enemySmallHeight = 1.5f;
        public float enemyLargeHeight = 3f;
        [Tooltip("地上武器立牌的宽度")] public float groundWeaponWidth = 3f;
        [Tooltip("拿在手里的武器长度（刀会短一点）")] public float heldWeaponLength = 2.2f;
    }
}
