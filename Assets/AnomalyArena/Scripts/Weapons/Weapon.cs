using UnityEngine;

namespace AnomalyArena
{
    public enum WeaponType { Gun, Knife, Missile }

    public enum EffectId { None, GunSwing, GunReverseShot, KnifeThrow, KnifeHook, MissileHoming, MissileSelfLaunch }

    /// <summary>
    /// 一把武器实例。生成时就定好效果；第一次被使用时揭晓；之后效果不再变，丢下再捡不重抽、不回复次数。
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public WeaponType type;
        public Transform visual;

        [Header("Debug")]
        [Tooltip("强制这把武器的效果（只在揭晓前生效，必须是同类武器的效果）")]
        public EffectId debugForceEffect = EffectId.None;

        [Header("Runtime (read only)")]
        [SerializeField] WeaponEffect effect;
        [SerializeField] bool revealed;
        [SerializeField] int usesLeft;
        [SerializeField] int maxUses;
        [Tooltip("当前弹夹里还剩几发（roundsPerUse > 1 的效果才有）")] [SerializeField] int roundsLeft;

        public WeaponEffect Effect => effect;
        public bool Revealed { get => revealed; set => revealed = value; }
        public int UsesLeft { get => usesLeft; set => usesLeft = value; }
        public int MaxUses => maxUses;
        /// <summary>弹夹余量：打空了下一发才扣一次次数。丢下再捡保留。</summary>
        public int RoundsLeft { get => roundsLeft; set => roundsLeft = value; }
        /// <summary>扔出去还没回来的飞刀；不为空时这把武器不能再用、也不会因次数用完而消失。</summary>
        public Projectile InFlight { get; set; }
        /// <summary>进行中的持续流程（钩子、冲刺）。不为空时武器不会消失。</summary>
        public WeaponRuntime Active { get; set; }
        public IWeaponHolder Holder { get; private set; }
        public bool OnGround => Holder == null;

        float phase;

        public static string TypeName(WeaponType t)
        {
            switch (t)
            {
                case WeaponType.Gun: return "Gun";
                case WeaponType.Knife: return "Knife";
                default: return "Missile";
            }
        }

        /// <summary>地上与界面显示的名字：“Gun ?” 或 “Gun · Reverse Shot”。</summary>
        public string Label => Revealed && effect ? $"{TypeName(type)} · {effect.displayName}" : $"{TypeName(type)} ?";

        // 美术版：地上是立牌，拿在手里是平放的贴图
        Transform groundArt, heldArt;
        float groundArtHeight;

        /// <summary>地上武器名字标签的锚点：美术版放在立牌顶上（沿镜头上方），不压在贴图上。</summary>
        public Vector3 LabelPoint(Camera cam)
        {
            if (groundArt && cam) return groundArt.position + cam.transform.up * (groundArtHeight + 0.45f);
            return transform.position + Vector3.up * 1.4f;
        }

        public void Init(WeaponEffect e, int uses)
        {
            effect = e;
            maxUses = usesLeft = uses;
            phase = Random.value * 10f;
            BuildArt();
        }

        void BuildArt()
        {
            if (!Art.On || !visual) return;
            var s = Art.Set;
            var tex = type == WeaponType.Gun ? s.gun : type == WeaponType.Knife ? s.knife : s.missileLauncher;
            if (!tex) return;
            Art.HideWhitebox(visual.gameObject);
            groundArtHeight = s.groundWeaponWidth / Art.Aspect(tex);
            groundArt = Art.Billboard(visual, tex, groundArtHeight, Art.OrderCharacter, out _);
            groundArt.localPosition = new Vector3(0f, -0.4f, 0f);
            float held = s.heldWeaponLength * (type == WeaponType.Knife ? 0.8f : 1f);
            heldArt = Art.Flat(visual, tex, held, Art.OrderHeld, out _, new Vector3(0f, 0f, held * 0.3f));
            SetArtHeld(false);
        }

        void SetArtHeld(bool held)
        {
            if (groundArt) groundArt.gameObject.SetActive(!held);
            if (heldArt) heldArt.gameObject.SetActive(held);
        }

        public void ApplyDebugForce()
        {
            if (Revealed || debugForceEffect == EffectId.None) return;
            var forced = GameManager.Instance.weapons.GetEffect(debugForceEffect);
            if (forced != null && forced.type == type) effect = forced;
        }

        public void AttachTo(IWeaponHolder holder, Transform hand)
        {
            Holder = holder;
            transform.SetParent(hand, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (visual)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
            }
            SetArtHeld(true);
        }

        public void PlaceOnGround(Vector3 pos)
        {
            Holder = null;
            Active = null;
            transform.SetParent(null, true);
            transform.position = new Vector3(pos.x, 0f, pos.z);
            transform.rotation = Quaternion.identity;
            SetArtHeld(false);
        }

        public void Consume() => Destroy(gameObject);

        void Update()
        {
            if (!OnGround || !visual) return;
            phase += Time.deltaTime;
            visual.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(phase * 3f) * 0.08f, 0f);
            visual.localRotation = Quaternion.Euler(0f, phase * 60f, 0f);
        }
    }
}
