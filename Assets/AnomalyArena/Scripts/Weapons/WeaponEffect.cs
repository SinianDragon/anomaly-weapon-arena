using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Base class for weapon effects. Each effect is a separate script with its values stored in a ScriptableObject asset, so tuning needs no code changes.
    /// Effects act on the user only through IWeaponHolder.
    /// </summary>
    public abstract class WeaponEffect : ScriptableObject
    {
        [Header("Identity")]
        public EffectId id;
        public WeaponType type;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Minimum interval between two uses")] public float cooldown = 0.3f;
        [Tooltip("While the left button is held, keeps using it automatically at the cooldown interval")] public bool holdToRepeat;
        [Tooltip("Shots per use (clip size); 1 = every shot spends a use")] [Min(1)] public int roundsPerUse = 1;

        public abstract void Use(Weapon weapon, IWeaponHolder user);
    }

    /// <summary>
    /// A routine that keeps running for a while after one use (hook, dash). The weapon does not disappear while it runs; uses are checked again when it ends.
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

        /// <summary>Left click again during the routine (hook: throw).</summary>
        public virtual void OnUsePressed() { }

        /// <summary>Left button released during the routine (Charge Swing: swing).</summary>
        public virtual void OnUseReleased() { }

        /// <summary>Right-click swap during the routine (hook: throws automatically first).</summary>
        public virtual void OnSwapAway() { }

        /// <summary>Forced cleanup on wave change or player death.</summary>
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

    /// <summary>Projectile (bullet, throwing knife, missile). Cleaned up together on wave change or player death.</summary>
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

        /// <summary>Even in whitebox the flight direction has to be readable: draws a trail line.</summary>
        protected TrailRenderer AddTrail(Color c, float width)
        {
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.sharedMaterial = GameManager.Instance.FxMat(c);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tr;
        }
    }
}
