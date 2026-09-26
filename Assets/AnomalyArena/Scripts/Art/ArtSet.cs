using UnityEngine;

namespace AnomalyArena
{
    /// <summary>美术版用到的贴图。只在标题画面切到美术版后才会用到；白盒版完全不读。某一项留空时，那一处退回白盒表现。</summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Art Set")]
    public class ArtSet : ScriptableObject
    {
        [Header("Characters")]
        public Texture2D player;
        public Texture2D enemySmall;
        public Texture2D enemyLarge;
        [Tooltip("蓄力时换上的发光版：四周留同样宽的边距，按“发光图尺寸 / 原图尺寸”放大后和原图同心重合")]
        public Texture2D enemySmallWindup;
        public Texture2D enemyLargeWindup;
        [Tooltip("被飞刀带回的尸体（背上插着刀、趴着）")] public Texture2D corpseSmall;
        public Texture2D corpseLarge;

        [Header("Weapons (ground / held)")]
        public Texture2D gun;
        public Texture2D knife;
        public Texture2D missileLauncher;

        [Header("Knife hook: hilt + tiled chain + knife tip")]
        public Texture2D hookHilt;
        [Tooltip("一节链条，沿长度方向重复平铺（导入方式要是 Repeat）")] public Texture2D hookMid;
        public Texture2D hookTip;

        [Header("Projectiles")]
        public Texture2D missile;
        [Tooltip("导弹尾焰")] public Texture2D flame;
        [Tooltip("发射自己时包着玩家的大火焰；留空就用导弹尾焰")] public Texture2D rocketFlame;
        [Tooltip("反向射击的子弹（弹头朝右，带拖尾）")] public Texture2D bullet;

        [Header("Effects")]
        [Tooltip("敌人挥拳的弧光（朝右鼓）")] public Texture2D enemyPunchArc;
        public Texture2D hitSpark;
        [Tooltip("冲刺、撞墙时的尘土")] public Texture2D dust;
        [Tooltip("爆炸动画，从小到大依次播放")] public Texture2D[] explosionFrames;
        [Tooltip("爆炸范围圈，扩散到真实的爆炸半径")] public Texture2D explosionRing;
        [Tooltip("受伤后保护期间罩在身上的泡泡")] public Texture2D shieldBubble;
        public Texture2D spawnXSmall;
        public Texture2D spawnXLarge;

        [Header("Arena")]
        public Texture2D floor;
        [Tooltip("每块地板贴图覆盖几格；越大花纹越少")] public float floorTileSize = 5f;
        [Tooltip("缺口边缘：上半沙地、下半黑坑")] public Texture2D gapEdge;
        [Tooltip("墙顶的砖（导入方式要是 Repeat）")] public Texture2D wallBrick;
        [Tooltip("每块墙砖贴图覆盖几格")] public float wallTileSize = 1.5f;

        [Header("Sizes (world units)")]
        // 角色立牌宽度大致和碰撞体直径对齐（玩家 / 小型 1 格，大型 2 格 = 小型的两倍），武器比角色显眼
        public float playerHeight = 1.4f;
        public float enemySmallHeight = 1.5f;
        public float enemyLargeHeight = 3f;
        [Tooltip("地上武器立牌的宽度")] public float groundWeaponWidth = 3f;
        [Tooltip("拿在手里的武器长度（刀会短一点）")] public float heldWeaponLength = 2.2f;
    }
}
