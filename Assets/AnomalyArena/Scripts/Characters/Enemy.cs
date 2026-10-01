using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Enemy: always chases the player and only starts winding up a punch when almost touching (body turns yellow). During wind-up its facing is locked and it stands still.
    /// After the wind-up it punches along the locked direction: the player only takes damage if still inside the punch arc, so walking away on yellow dodges it. Touching the player does no damage.
    /// If an enemy hooked by the player is inside the punch arc, it is hit first (cover). Enemies do not pick up weapons in this version.
    /// </summary>
    public class Enemy : Combatant
    {
        [Header("Attack")]
        public float attackDamage = 5f;
        [Tooltip("How close to the player's body edge before winding up (almost touching)")] public float triggerRange = 0.3f;
        [Tooltip("Hit distance when the punch lands, from the body edge; the player dodges by leaving this range during the wind-up")] public float attackRange = 0.8f;
        [Tooltip("Arc of the punch in degrees; facing is locked when the wind-up starts")] public float attackArc = 120f;
        public float windupTime = 0.5f;
        public float attackInterval = 1f;
        [Tooltip("How far the player is knocked back when hit")] public float attackKnockback = 5f;

        [Header("Look")]
        public Material normalMaterial;
        public Material windupMaterial;

        [Header("Type")]
        [Tooltip("Large enemy: drops weapons, wider HP bar, uses the large textures (no longer decided by radius)")] public bool large;

        public bool IsLarge => large;
        public bool WindingUp => windup;
        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;
        protected override Texture2D ArtTexture => Art.Set ? (IsLarge ? Art.Set.enemyLarge : Art.Set.enemySmall) : null;
        protected override float ArtHeight => IsLarge ? Art.Set.enemyLargeHeight : Art.Set.enemySmallHeight;
        protected override Texture2D ArtWindupTexture => Art.Set ? (IsLarge ? Art.Set.enemyLargeWindup : Art.Set.enemySmallWindup) : null;

        Vector3 facing = Vector3.back;
        bool windup;
        float windupTimer;
        float cooldown;

        protected override void TickNormal(float dt)
        {
            if (cooldown > 0f) cooldown -= dt;
            var p = GM.player;
            if (GM.State != GameState.Playing || p == null || !p.IsAlive)
            {
                Stop();
                CancelWindup();
                return;
            }

            if (windup)
            {
                // Facing is locked; it no longer turns with the player
                Stop();
                windupTimer -= dt;
                // Blink during wind-up: pulse the glowing billboard if there is one, otherwise tint the billboard yellow
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                if (artWindupSprite) Art.Tint(artWindupSprite, Color.Lerp(Color.white, new Color(1.35f, 1.3f, 1.1f), pulse));
                else if (artSprite) Art.Tint(artSprite, Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), pulse));
                if (windupTimer <= 0f)
                {
                    PerformAttack();
                    CancelWindup();
                    cooldown = attackInterval;
                }
                return;
            }

            Vector3 to = Query.Flat(p.Position - Position);
            float dist = to.magnitude;
            if (dist > 0.01f) facing = to / dist;
            float edge = dist - radius - p.Radius;

            if (edge <= triggerRange && cooldown <= 0f)
            {
                windup = true;
                windupTimer = windupTime;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 v = edge > triggerRange ? facing * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void PerformAttack()
        {
            var arc = Art.On ? Art.Set.enemyPunchArc : null;
            if (arc)
            {
                // Illustrated mode: an arc flash in front of the fist (right side of the texture = punch direction), about as long as the punch range
                float reach = radius + attackRange;
                Fx.SpriteFlat(arc, Query.AtCastHeight(Position) + facing * (reach * 0.7f), facing, reach * 1.4f, 0.2f);
            }
            else
            {
                Fx.Sector(Position, facing, radius + attackRange, attackArc, new Color(1f, 0.3f, 0.2f, 0.55f));
            }
            var p = GM.player;
            var shield = p.HeldShield;
            if (shield != null && shield != this && InFist(shield))
            {
                // Hitting cover does not knock it back (distance 0), but a direction is passed so shards fly outward
                shield.ReceiveDamage(DamageInfo.Attack(attackDamage, this, shield.Position - Position));
                return;
            }
            if (p.IsAlive && InFist(p))
                p.ReceiveDamage(DamageInfo.Attack(attackDamage, this, p.Position - Position, attackKnockback));
        }

        /// <summary>Target is inside the punch arc: edge distance within attackRange and within attackArc/2 of the locked direction (angle ignored when touching).</summary>
        bool InFist(Combatant c)
        {
            Vector3 to = Query.Flat(c.Position - Position);
            float d = to.magnitude;
            if (d - radius - c.Radius > attackRange) return false;
            return d <= radius + c.Radius * 0.5f || Vector3.Angle(facing, to) <= attackArc * 0.5f;
        }

        /// <summary>
        /// When walking on its own it never walks off into a gap; it only falls when knocked back or thrown.
        /// If the next step would leave the platform (less than a body from the edge), it first tries moving along x only or z only (sliding along the edge), and stops if neither works.
        /// </summary>
        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (ArenaShape.ContainsWithMargin(Position + v * dt, radius)) return v;
            var alongX = new Vector3(v.x, 0f, 0f);
            if (ArenaShape.ContainsWithMargin(Position + alongX * dt, radius)) return alongX;
            var alongZ = new Vector3(0f, 0f, v.z);
            if (ArenaShape.ContainsWithMargin(Position + alongZ * dt, radius)) return alongZ;
            return Vector3.zero;
        }

        void Stop() => rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        void CancelWindup()
        {
            if (!windup) return;
            windup = false;
            SetLook(false);
        }

        void SetLook(bool yellow)
        {
            if (bodyRenderer) bodyRenderer.sharedMaterial = yellow && windupMaterial ? windupMaterial : normalMaterial;
            if (ShowArtWindup(yellow)) return;
            if (!yellow && artSprite) Art.Tint(artSprite, Color.white);
        }

        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Normal) CancelWindup();
        }

        protected override void OnDied()
        {
            Fx.Pop(Position + Vector3.up * radius, bodyRenderer ? bodyRenderer.sharedMaterial.color : Color.red, radius * 3f);
            Destroy(gameObject);
        }

        protected override void OnFallFinished() => Destroy(gameObject);
    }
}
