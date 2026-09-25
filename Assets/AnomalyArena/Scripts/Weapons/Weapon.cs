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

        public WeaponEffect Effect => effect;
        public bool Revealed { get => revealed; set => revealed = value; }
        public int UsesLeft { get => usesLeft; set => usesLeft = value; }
        public int MaxUses => maxUses;
        /// <summary>进行中的持续流程（钩子、冲刺）。不为空时武器不会消失。</summary>
        public WeaponRuntime Active { get; set; }
        public IWeaponHolder Holder { get; private set; }
        public bool OnGround => Holder == null;

        float phase;

        public static string TypeName(WeaponType t) => t.ToString();

        /// <summary>地上与界面显示的名字：“Gun ?” 或 “Gun · Reverse Shot”。</summary>
        public string Label => Revealed && effect ? $"{TypeName(type)} · {effect.displayName}" : $"{TypeName(type)} ?";

        public void Init(WeaponEffect e, int uses)
        {
            effect = e;
            maxUses = usesLeft = uses;
            phase = Random.value * 10f;
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
        }

        public void PlaceOnGround(Vector3 pos)
        {
            Holder = null;
            Active = null;
            transform.SetParent(null, true);
            transform.position = new Vector3(pos.x, 0f, pos.z);
            transform.rotation = Quaternion.identity;
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
