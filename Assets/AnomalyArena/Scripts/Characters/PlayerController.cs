using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>Player: WASD to move, Shift to sprint (uses stamina), mouse to aim, left button to use the weapon (punch when unarmed; hold for repeating weapons), right button to swap weapons.</summary>
    public class PlayerController : Combatant
    {
        [Header("Punch (unarmed basic attack)")] public float punchDamage = 3f;
        [Tooltip("Measured from the body edge")] public float punchRange = 1.2f;
        public float punchArc = 90f;
        public float punchKnockback = 3f;
        public float punchCooldown = 0.4f;

        [Header("Sprint")] public Stamina stamina = new Stamina();

        // Interval between dust puffs while sprinting (seconds)
        const float SprintDustInterval = 0.07f;

        Vector3 aim = Vector3.forward;
        Vector2 moveInput;
        float sprintMultiplier = 1f;
        float dustTimer;

        /// <summary>For automated tests: replaces the keyboard, forcing the sprint key held / released.</summary>
        [System.NonSerialized] public bool? debugSprintHeld;

        /// <summary>For automated tests: locks the aim direction so it no longer follows the mouse.</summary>
        [System.NonSerialized] public bool debugLockAim;

        public Weapon NearPickup { get; private set; }
        public override Team Team => Team.Player;
        public override Vector3 AimDirection => aim;
        protected override Texture2D ArtTexture => Art.Set ? Art.Set.player : null;
        protected override float ArtHeight => Art.Set.playerHeight;

        void Update()
        {
            if (!IsAlive || GM == null) return;
            UpdateAim();

            bool playing = GM.State == GameState.Playing;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            moveInput = Vector2.zero;
            if (playing && kb != null)
            {
                if (kb.wKey.isPressed) moveInput.y += 1f;
                if (kb.sKey.isPressed) moveInput.y -= 1f;
                if (kb.dKey.isPressed) moveInput.x += 1f;
                if (kb.aKey.isPressed) moveInput.x -= 1f;
                if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            }

            TickSprint(playing, kb);
            if (!playing || mouse == null) return;

            HandlePickup(mouse.rightButton.wasPressedThisFrame);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Weapon == null) Punch();
                else TryUseWeapon();
            }
            // Repeating weapons: holding keeps using it at the cooldown interval (TryUseWeapon returns early while on cooldown)
            else if (mouse.leftButton.isPressed && Weapon != null && Weapon.Effect && Weapon.Effect.holdToRepeat &&
                     Weapon.Revealed)
            {
                TryUseWeapon();
            }

            if (mouse.leftButton.wasReleasedThisFrame) ReleaseWeapon();
        }

        /// <summary>Sprint: while Shift is held and the player is moving, speed ×2 and stamina drains; it recovers when not sprinting.</summary>
        void TickSprint(bool playing, Keyboard kb)
        {
            bool held = debugSprintHeld ?? (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed));
            bool moving = moveInput.sqrMagnitude > 0.01f || debugSprintHeld == true;
            bool want = playing && held && moving && State == CharacterState.Normal && !MovementLocked;
            sprintMultiplier = stamina.Tick(Time.deltaTime, want);
            if (!stamina.Sprinting) return;
            dustTimer -= Time.deltaTime;
            if (dustTimer > 0f) return;
            dustTimer = SprintDustInterval;
            var feet = new Vector3(Position.x, 0.2f, Position.z);
            var dust = Art.On ? Art.Set.dust : null;
            if (dust) Fx.SpriteBillboard(dust, feet, 0.4f, 1.1f, 0.4f);
            else Fx.Pop(feet, new Color(0.85f, 0.85f, 0.8f), 0.9f);
        }

        void UpdateAim()
        {
            var mouse = Mouse.current;
            var cam = GM.cam;
            if (debugLockAim || mouse == null || cam == null) return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, Query.CastHeight, 0f));
            if (!plane.Raycast(ray, out float t)) return;
            Vector3 d = Query.Flat(ray.GetPoint(t) - Position);
            if (d.sqrMagnitude > 0.01f) aim = d.normalized;
        }

        public void DebugSetAim(Vector3 dir)
        {
            debugLockAim = true;
            aim = Query.Flat(dir).normalized;
        }

        protected override void TickNormal(float dt)
        {
            Vector3 v = Vector3.zero;
            if (!MovementLocked && GM.State == GameState.Playing)
                v = new Vector3(moveInput.x, 0f, moveInput.y) * (moveSpeed * SpeedMultiplier * sprintMultiplier);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <summary>Unarmed punch: hits only the nearest character in a small arc in front, dealing damage and knockback.</summary>
        void Punch()
        {
            if (State != CharacterState.Normal || weaponCooldown > 0f) return;
            weaponCooldown = punchCooldown;
            float reach = radius + punchRange;
            Fx.Sector(Position, aim, reach, punchArc, new Color(1f, 1f, 1f, 0.5f));
            var c = Query.MeleeTarget(this, Position, aim, reach, punchArc);
            if (c == null) return;
            Vector3 to = Query.Flat(c.Position - Position);
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : aim;
            c.ReceiveDamage(DamageInfo.Attack(punchDamage, this, dir, punchKnockback));
        }

        void HandlePickup(bool rightClick)
        {
            var spawner = GM.weapons;
            NearPickup = spawner.FindNear(Position);
            if (NearPickup == null) return;

            if (Weapon == null)
            {
                if (State == CharacterState.Normal) PickUp(NearPickup);
                return;
            }

            if (!rightClick || State != CharacterState.Normal) return;
            if (Weapon.Active != null && !Weapon.Active.CanSwap) return;

            var target = NearPickup;
            // While holding a hooked enemy: throw it along the current direction first, then swap; no extra use is spent
            if (Weapon.Active != null) Weapon.Active.OnSwapAway();
            if (Weapon != null)
            {
                var old = Unequip();
                spawner.Drop(old, Position);
            }

            PickUp(target);
        }

        void PickUp(Weapon w)
        {
            GM.weapons.Take(w);
            Equip(w);
            NearPickup = null;
        }

        protected override void OnWeaponRevealed(Weapon w) => GM.hud.Reveal(w);

        protected override void OnDamaged(float amount) => GM.hud.FlashDamage();

        protected override void OnDied()
        {
            if (bodyRenderer) bodyRenderer.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
            if (artPivot) artPivot.localScale = new Vector3(1.3f, 0.3f, 1f);
        }
    }
}