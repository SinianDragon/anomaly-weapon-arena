using UnityEngine;

namespace AnomalyArena
{
    public enum WeaponType { Gun, Knife, Missile }

    public enum EffectId { None, GunSwing, GunReverseShot, KnifeThrow, KnifeHook, MissileHoming, MissileSelfLaunch }

    /// <summary>
    /// One weapon instance. Its effect is decided when it spawns and revealed on first use; after that the effect never changes, and dropping and picking it up again neither rerolls it nor restores uses.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public WeaponType type;
        public Transform visual;

        [Header("Debug")]
        [Tooltip("Forces this weapon's effect (only before reveal; must be an effect of the same weapon type)")]
        public EffectId debugForceEffect = EffectId.None;

        [Header("Runtime (read only)")]
        [SerializeField] WeaponEffect effect;
        [SerializeField] bool revealed;
        [SerializeField] int usesLeft;
        [SerializeField] int maxUses;
        [Tooltip("Rounds left in the current clip (only for effects with roundsPerUse > 1)")] [SerializeField] int roundsLeft;

        public WeaponEffect Effect => effect;
        public bool Revealed { get => revealed; set => revealed = value; }
        public int UsesLeft { get => usesLeft; set => usesLeft = value; }
        public int MaxUses => maxUses;
        /// <summary>Rounds left in the clip: a use is spent only on the next shot after it is empty. Kept when dropped and picked up again.</summary>
        public int RoundsLeft { get => roundsLeft; set => roundsLeft = value; }
        /// <summary>The thrown knife that has not returned yet; while not null this weapon cannot be used and does not disappear when out of uses.</summary>
        public Projectile InFlight { get; set; }
        /// <summary>The ongoing routine (hook, dash). While not null the weapon does not disappear.</summary>
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

        /// <summary>Name shown on the ground and in the UI: 'Gun ?' or 'Gun · Reverse Shot'.</summary>
        public string Label => Revealed && effect ? $"{TypeName(type)} · {effect.displayName}" : $"{TypeName(type)} ?";

        // Illustrated mode: a billboard on the ground, a flat texture when held
        Transform groundArt, heldArt;
        float groundArtHeight;

        /// <summary>Anchor for the name label of a weapon on the ground: in illustrated mode it sits on top of the billboard (along the camera's up) so it does not cover the texture.</summary>
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
