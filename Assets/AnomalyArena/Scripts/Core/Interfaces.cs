using UnityEngine;

namespace AnomalyArena
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>States of the character state machine. Enter / exit logic for each state is in Combatant.SetState.</summary>
    public enum CharacterState
    {
        Normal, // free to act
        Knocked, // knocked back: no control, wall hits deal damage
        Thrown, // thrown by the hook: same as above, distance not affected by body size
        Hooked, // hooked: cannot move or attack, position controlled by the hook, cannot fall into a gap
        Dashing, // human-missile dash: immune to attack damage and knockback
        Falling, // fell into a gap: already counted as eliminated / lost, only the fall is left to play
        Dead,
    }

    public enum KnockKind
    {
        Hit, // normal knockback: distance scaled by body size, wall hits deal damage
        Throw, // hook throw: fixed distance, wall hits deal damage
        Push, // recoil push: wall hits deal no damage
    }

    public enum DeathCause
    {
        None,
        HpDepleted,
        FellIntoGap
    }

    /// <summary>Kind of hit effect: decides shard color, count and whether there is a shock ring.</summary>
    public enum HitKind
    {
        Blunt, // punch, swing
        Bullet, // bullet
        Pierce, // throwing knife, human missile passing through
        Slam, // wall impact, hit by a corpse
        Blast, // explosion
    }

    public struct DamageInfo
    {
        public float amount;

        /// <summary>Attack damage is subject to the 0.5 s protection and dash immunity; wall damage does not go through here.</summary>
        public bool isAttack;

        /// <summary>Knockback direction; when zero, hit shards fly in random directions.</summary>
        public Vector3 knockDir;

        public float knockDistance;
        public IDamageDealer dealer;
        public HitKind kind;

        public static DamageInfo Attack(float amount, IDamageDealer dealer, Vector3 knockDir = default,
            float knockDistance = 0f,
            HitKind kind = HitKind.Blunt) =>
            new DamageInfo
            {
                amount = amount, isAttack = true, dealer = dealer, knockDir = knockDir, knockDistance = knockDistance,
                kind = kind
            };
    }

    public interface IDamageDealer
    {
        IWeaponHolder Owner { get; }
    }

    public interface IDamageReceiver
    {
        bool IsAlive { get; }

        /// <returns>Whether the damage actually applied (false while protected / dashing)</returns>
        bool ReceiveDamage(DamageInfo info);
    }

    /// <summary>
    /// A 'weapon holder': can hold a weapon, be knocked back, take damage, and has a facing.
    /// Weapon effects act on the user only through this interface, so weapon code will not need changes when enemies pick up weapons later.
    /// </summary>
    public interface IWeaponHolder : IDamageReceiver
    {
        Transform Root { get; }
        Vector3 Position { get; }
        Vector3 AimDirection { get; }
        float Radius { get; }
        Team Team { get; }
        Weapon Weapon { get; }
        CharacterState State { get; }

        void Knockback(Vector3 dir, float distance, KnockKind kind);

        /// <summary>Push added on top of own movement (burst-fire recoil): does not enter the knocked state or interrupt input, no wall damage, but can still push the holder into a gap.</summary>
        void Push(Vector3 dir, float distance);

        /// <summary>Wall slam (human missile): teleport to at, take wall damage once, then bounce bounceDistance toward away.</summary>
        void SlamIntoWall(Vector3 at, Vector3 away, float bounceDistance);

        void SetState(CharacterState state);
        void SetMovePosition(Vector3 position);
        void Kill();

        /// <summary>Called when a weapon's ongoing routine (hook, dash) ends, to handle a weapon whose uses have run out.</summary>
        void OnWeaponRuntimeFinished();
    }
}