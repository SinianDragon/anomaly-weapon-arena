using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 武器效果的基类。每种效果是一个单独的脚本，数值存在 ScriptableObject 资源里，调数值不用改代码。
    /// 效果只通过 IWeaponHolder 操作使用者。
    /// </summary>
    public abstract class WeaponEffect : ScriptableObject
    {
        [Header("Identity")]
        public EffectId id;
        public WeaponType type;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("两次使用之间的最短间隔")] public float cooldown = 0.3f;

        public abstract void Use(Weapon weapon, IWeaponHolder user);
    }

    /// <summary>
    /// 一次使用后还要持续一段时间的流程（钩子、冲刺）。进行中武器不会消失，结束后再检查次数。
    /// </summary>
    public abstract class WeaponRuntime : MonoBehaviour
    {
        protected Weapon weapon;
        protected IWeaponHolder user;
        bool finished;

        public virtual bool LocksMovement => false;
        public virtual float SpeedMultiplier => 1f;
        public virtual bool CanSwap => false;

        protected void Bind(Weapon w, IWeaponHolder u)
        {
            weapon = w;
            user = u;
            if (w != null) w.Active = this;
            GameManager.Instance.Register(this);
        }

        /// <summary>流程中再按左键（钩子：投掷）。</summary>
        public virtual void OnUsePressed() { }

        /// <summary>流程中按右键换武器（钩子：先自动扔出去）。</summary>
        public virtual void OnSwapAway() { }

        /// <summary>切换波次、玩家死亡时强制清理。</summary>
        public virtual void Cancel() => Finish();

        protected void Finish()
        {
            if (finished) return;
            finished = true;
            if (weapon != null && weapon.Active == this) weapon.Active = null;
            GameManager.Instance.Unregister(this);
            Destroy(gameObject);
            if (user != null) user.OnWeaponRuntimeFinished();
        }
    }

    /// <summary>飞行物（子弹、飞刀、导弹）。切换波次、玩家死亡时统一清掉。</summary>
    public abstract class Projectile : MonoBehaviour, IDamageDealer
    {
        public IWeaponHolder Owner { get; protected set; }

        protected virtual void Awake() => GameManager.Instance.Register(this);

        protected virtual void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.Unregister(this);
        }

        protected static Transform MakeVisual(Transform parent, PrimitiveType t, Vector3 scale, Color c, Vector3 localPos = default)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            go.GetComponent<Renderer>().sharedMaterial = GameManager.Instance.Mat(c);
            return go.transform;
        }

        /// <summary>白盒阶段也要看得出往哪飞：拖一条线。</summary>
        protected void AddTrail(Color c, float width)
        {
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.sharedMaterial = GameManager.Instance.FxMat(c);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
