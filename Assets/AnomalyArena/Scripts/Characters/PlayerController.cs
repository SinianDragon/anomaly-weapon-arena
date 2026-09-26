using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>玩家：WASD 移动、Shift 冲刺（耗体力）、鼠标瞄准、左键用武器（空手时出拳，连发武器可按住）、右键换武器。</summary>
    public class PlayerController : Combatant
    {
        [Header("Punch (空手基础攻击)")] public float punchDamage = 3f;
        [Tooltip("从身体边缘算")] public float punchRange = 1.2f;
        public float punchArc = 90f;
        public float punchKnockback = 3f;
        public float punchCooldown = 0.4f;

        [Header("Sprint")] public Stamina stamina = new Stamina();

        // 冲刺时脚下冒尘土的间隔（秒）
        const float SprintDustInterval = 0.07f;

        Vector3 aim = Vector3.forward;
        Vector2 moveInput;
        float sprintMultiplier = 1f;
        float dustTimer;

        /// <summary>自动化测试用：代替键盘，强制按住 / 松开冲刺键。</summary>
        [System.NonSerialized] public bool? debugSprintHeld;

        /// <summary>自动化测试用：锁定瞄准方向，不再跟随鼠标。</summary>
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
            // 连发武器：按住就按冷却间隔继续用（冷却没到时 TryUseWeapon 直接返回）
            else if (mouse.leftButton.isPressed && Weapon != null && Weapon.Effect && Weapon.Effect.holdToRepeat &&
                     Weapon.Revealed)
            {
                TryUseWeapon();
            }

            if (mouse.leftButton.wasReleasedThisFrame) ReleaseWeapon();
        }

        /// <summary>冲刺：按住 Shift 且在移动时，速度 ×2、消耗体力；不冲刺时回复。</summary>
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

        /// <summary>空手出拳：只打前方小扇形里最近的一个人，扣血并撞飞。</summary>
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
            if (artPivot) artPivot.localScale = new Vector3(1.3f, 0.3f, 1f);
        }
    }
}