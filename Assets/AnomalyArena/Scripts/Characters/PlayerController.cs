using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>玩家：WASD 移动、鼠标瞄准、左键用武器（空手时出拳）、右键换武器。</summary>
    public class PlayerController : Combatant
    {
        [Header("Punch (空手基础攻击)")]
        public float punchDamage = 3f;
        [Tooltip("从身体边缘算")] public float punchRange = 1.2f;
        public float punchArc = 90f;
        public float punchKnockback = 3f;
        public float punchCooldown = 0.4f;

        Vector3 aim = Vector3.forward;
        Vector2 moveInput;

        /// <summary>自动化测试用：锁定瞄准方向，不再跟随鼠标。</summary>
        [System.NonSerialized] public bool debugLockAim;

        public Weapon NearPickup { get; private set; }
        public override Team Team => Team.Player;
        public override Vector3 AimDirection => aim;

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
            if (!playing || mouse == null) return;

            HandlePickup(mouse.rightButton.wasPressedThisFrame);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Weapon == null) Punch();
                else TryUseWeapon();
            }
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
                v = new Vector3(moveInput.x, 0f, moveInput.y) * (moveSpeed * SpeedMultiplier);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <summary>空手出拳：前方小扇形，扣血并撞飞。</summary>
        void Punch()
        {
            if (State != CharacterState.Normal || weaponCooldown > 0f) return;
            weaponCooldown = punchCooldown;
            float reach = radius + punchRange;
            Fx.Sector(Position, aim, reach, punchArc, new Color(1f, 1f, 1f, 0.5f));
            foreach (var c in Query.Characters(Query.AtCastHeight(Position), reach + 2f))
            {
                if (c == this || !c.IsAlive) continue;
                Vector3 to = Query.Flat(c.Position - Position);
                if (to.magnitude - c.Radius > reach) continue;
                if (to.magnitude > c.Radius && Vector3.Angle(aim, to) > punchArc * 0.5f) continue;
                Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : aim;
                c.ReceiveDamage(DamageInfo.Attack(punchDamage, this, dir, punchKnockback));
            }
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
            // 钩着敌人时：先朝当前方向自动扔出去，再换，不额外扣次
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
        }
    }
}
