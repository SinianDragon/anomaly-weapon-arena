using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Hook routine: extend → pull (swings with the mouse) → hold (cover) → throw, or miss → retract.
    /// State enter / exit:
    ///   Extend  enter: left click. exit: hooked a character → Pull; hit a wall or fully extended → Retract.
    ///   Pull    enter: hooked a character, target enters Hooked. exit: pulled in front → Hold; target invalid → Retract.
    ///   Hold    enter: pulled in front. exit: left click or right-click swap → throw and end; target dies → end.
    ///   Retract enter: missed. exit: retracted to 0 → end.
    /// </summary>
    public class Hook : WeaponRuntime
    {
        enum Phase
        {
            Extend,
            Pull,
            Hold,
            Retract
        }

        KnifeHookEffect cfg;
        Phase phase;
        Vector3 dir;
        float length;
        float pullDistance;
        Combatant target;

        public Combatant HeldTarget => phase == Phase.Hold && Valid() ? target : null;
        public override bool LocksMovement => phase != Phase.Hold;
        public override float SpeedMultiplier => phase == Phase.Hold ? cfg.dragSpeedMultiplier : 1f;
        public override bool CanSwap => phase == Phase.Hold;

        public void Begin(KnifeHookEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            dir = u.AimDirection;
            phase = Phase.Extend;
            if (Art.On && Art.Set.hookMid)
            {
                // Illustrated mode: hilt and tip stay fixed, the middle stretches with length; the whitebox box is hidden
                GetComponent<Renderer>().enabled = false;
                blade = new StretchBlade(Art.Set, 0.55f);
            }

            UpdateBlade();
        }

        StretchBlade blade;

        void OnDestroy() => blade?.Destroy();

        bool Valid() => target != null && target.IsAlive && target.State == CharacterState.Hooked;

        float HoldDistance => user.Radius + target.Radius + 0.1f;

        void FixedUpdate()
        {
            if (user == null || !user.IsAlive)
            {
                Cancel();
                return;
            }

            float dt = Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Extend:
                {
                    length = Mathf.Min(length + cfg.extendSpeed * dt, cfg.range);
                    Vector3 origin = Query.AtCastHeight(user.Position);
                    int mask = GameManager.WallMask | GameManager.CharacterMask;
                    // Starts inside the user's body; SphereCast ignores the user automatically
                    if (Physics.SphereCast(origin, cfg.hookRadius, dir, out var hit, length, mask,
                            QueryTriggerInteraction.Ignore))
                    {
                        var c = Combatant.From(hit.collider);
                        if (c != null && (IWeaponHolder)c != user && c.IsAlive && c.State != CharacterState.Hooked)
                        {
                            target = c;
                            c.SetState(CharacterState.Hooked);
                            pullDistance = Query.Flat(c.Position - user.Position).magnitude;
                            phase = Phase.Pull;
                        }
                        else if (c == null || (IWeaponHolder)c != user)
                        {
                            length = hit.distance;
                            phase = Phase.Retract;
                        }
                    }
                    else if (length >= cfg.range)
                    {
                        phase = Phase.Retract;
                    }

                    break;
                }
                case Phase.Pull:
                {
                    if (!Valid())
                    {
                        Release();
                        phase = Phase.Retract;
                        break;
                    }

                    // Moving the mouse while pulling swings the enemy to that side; it cannot fall into a gap while being pulled (Hooked ignores FallZone)
                    pullDistance = Mathf.MoveTowards(pullDistance, HoldDistance, cfg.pullSpeed * dt);
                    dir = user.AimDirection;
                    PlaceTarget(pullDistance);
                    if (pullDistance <= HoldDistance + 1e-3f) phase = Phase.Hold;
                    break;
                }
                case Phase.Hold:
                {
                    if (!Valid())
                    {
                        target = null;
                        Finish();
                        return;
                    }

                    dir = user.AimDirection;
                    PlaceTarget(HoldDistance);
                    break;
                }
                case Phase.Retract:
                    length -= cfg.retractSpeed * dt;
                    if (length <= 0f)
                    {
                        Finish();
                        return;
                    }

                    break;
            }

            UpdateBlade();
        }

        void PlaceTarget(float distance)
        {
            // When pulled in front, the target is never placed outside the platform (inside a wall or beyond a gap)
            Vector3 p = ArenaShape.ClampInside(user.Position + dir * distance, target.Radius);
            p.y = target.Position.y;
            target.SetMovePosition(p);
            length = Query.Flat(p - user.Position).magnitude;
        }

        public override void OnUsePressed()
        {
            if (phase == Phase.Hold) Throw();
        }

        public override void OnSwapAway()
        {
            if (phase == Phase.Hold) Throw();
        }

        /// <summary>Throws it along the current aim direction; the throw itself deals no damage; a wall hit deals wall damage as usual, and flying into a gap means falling.</summary>
        void Throw()
        {
            var t = target;
            target = null;
            if (t != null && t.IsAlive && t.State == CharacterState.Hooked)
            {
                t.SetState(CharacterState.Normal);
                t.Knockback(user.AimDirection, cfg.throwDistance, KnockKind.Throw);
            }

            Finish();
        }

        void Release()
        {
            if (target != null && target.State == CharacterState.Hooked) target.SetState(CharacterState.Normal);
            target = null;
        }

        public override void Cancel()
        {
            Release();
            Finish();
        }

        void UpdateBlade()
        {
            Vector3 origin = Query.AtCastHeight(user.Position);
            float len = Mathf.Max(0.05f, length);
            transform.position = origin + dir * (len * 0.5f);
            transform.rotation = Quaternion.LookRotation(dir);
            transform.localScale = new Vector3(0.12f, 0.06f, len);
            blade?.Set(origin, dir, len);
        }
    }
}