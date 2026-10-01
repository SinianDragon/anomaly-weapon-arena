using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Textures used by illustrated mode. Only read after switching to illustrated mode on the title screen; whitebox mode never touches them. If an entry is empty, that element falls back to its whitebox look.</summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Art Set")]
    public class ArtSet : ScriptableObject
    {
        [Header("Characters")]
        public Texture2D player;
        public Texture2D enemySmall;
        public Texture2D enemyLarge;
        [Tooltip("Glowing version shown during wind-up: same margin on every side, so scaling by 'glow image size / base image size' keeps it concentric with the base")]
        public Texture2D enemySmallWindup;
        public Texture2D enemyLargeWindup;
        [Tooltip("Corpse carried back by the throwing knife (face down, knife in its back)")] public Texture2D corpseSmall;
        public Texture2D corpseLarge;

        [Header("Weapons (ground / held)")]
        public Texture2D gun;
        public Texture2D knife;
        public Texture2D missileLauncher;

        [Header("Knife hook: hilt + tiled chain + knife tip")]
        public Texture2D hookHilt;
        [Tooltip("One chain link, tiled along the length (must be imported as Repeat)")] public Texture2D hookMid;
        public Texture2D hookTip;

        [Header("Projectiles")]
        public Texture2D missile;
        [Tooltip("Missile exhaust flame")] public Texture2D flame;
        [Tooltip("Large flame around the player during Launch Yourself; falls back to the missile flame if empty")] public Texture2D rocketFlame;
        [Tooltip("Reverse Shot bullet (tip pointing right, with a trail)")] public Texture2D bullet;

        [Header("Effects")]
        [Tooltip("Arc flash of an enemy punch (bulging to the right)")] public Texture2D enemyPunchArc;
        public Texture2D hitSpark;
        [Tooltip("Dust for sprinting and wall impacts")] public Texture2D dust;
        [Tooltip("Explosion animation frames, played from small to large")] public Texture2D[] explosionFrames;
        [Tooltip("Explosion range ring, expanding to the real blast radius")] public Texture2D explosionRing;
        [Tooltip("Bubble shown around a character during post-hit protection")] public Texture2D shieldBubble;
        public Texture2D spawnXSmall;
        public Texture2D spawnXLarge;

        [Header("Arena")]
        public Texture2D floor;
        [Tooltip("How many units one floor tile covers; larger = less pattern")] public float floorTileSize = 5f;
        [Tooltip("Gap edge: sand on the top half, black pit on the bottom half")] public Texture2D gapEdge;
        [Tooltip("Bricks on the wall tops (must be imported as Repeat)")] public Texture2D wallBrick;
        [Tooltip("How many units one wall brick tile covers")] public float wallTileSize = 1.5f;

        [Header("Sizes (world units)")]
        // Character billboard widths roughly match the collider diameter (player / small 1 unit, large 2 units = twice the small); weapons stand out more than characters
        public float playerHeight = 1.4f;
        public float enemySmallHeight = 1.5f;
        public float enemyLargeHeight = 3f;
        [Tooltip("Width of a weapon billboard on the ground")] public float groundWeaponWidth = 3f;
        [Tooltip("Length of a held weapon (the knife is a little shorter)")] public float heldWeaponLength = 2.2f;
    }
}
