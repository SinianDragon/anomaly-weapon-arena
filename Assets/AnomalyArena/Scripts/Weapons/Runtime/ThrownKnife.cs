using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 飞刀：直线飞出，不转弯。飞满距离、撞墙或打中人时开始返回；开始返回那一刻记下使用者的位置，之后朝这个点飞。
    /// 带着尸体返回时，路上碰到的人（包括使用者）按尸体生前的攻击伤害扣血并被撞飞；空刀不伤人。
    /// </summary>
    public class ThrownKnife : Projectile
    {
        KnifeThrowEffect cfg;
        Vector3 pos, dir;
        float traveled;
        bool returning;
        Vector3 lockPoint;
        float corpseDamage;
        float corpseRadius;
        Transform corpse;
        GameObject lockMarker;
        readonly HashSet<Combatant> hitOnReturn = new HashSet<Combatant>();

        public void Launch(KnifeThrowEffect e, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            cfg = e;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            MakeVisual(transform, PrimitiveType.Cube, new Vector3(0.5f, 0.08f, 1f), new Color(0.82f, 0.88f, 0.94f));
            AddTrail(new Color(0.85f, 0.9f, 1f, 0.7f), 0.12f);
            UpdateTransform();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!returning)
            {
                float step = cfg.speed * dt;
                Vector3 next = pos + dir * step;
                if (Query.WallBetween(pos, next, out var wall))
                {
                    pos += dir * Mathf.Max(0f, wall.distance - 0.1f);
                    StartReturn();
                }
                else
                {
                    pos = next;
                    traveled += step;
                    foreach (var c in Query.Characters(pos, 0.35f))
                    {
                        if ((IWeaponHolder)c == Owner || !c.IsAlive) continue;
                        HitFirst(c);
                        StartReturn();
                        break;
                    }
                    if (!returning && traveled >= cfg.range) StartReturn();
                }
            }
            else
            {
                Vector3 to = Query.Flat(lockPoint - pos);
                float step = cfg.returnSpeed * dt;
                if (to.magnitude <= step)
                {
                    pos = lockPoint;
                    Destroy(gameObject); // 尸体到达返回点后消失
                    return;
                }
                dir = to.normalized;
                pos += dir * step;
                if (corpse != null)
                {
                    foreach (var c in Query.Characters(pos, corpseRadius + 0.1f))
                    {
                        if (!c.IsAlive || !hitOnReturn.Add(c)) continue;
                        c.ReceiveDamage(DamageInfo.Attack(corpseDamage, this, dir, cfg.corpseKnockback));
                    }
                }
            }
            UpdateTransform();
        }

        void HitFirst(Combatant c)
        {
            float dmg = c.CorpseDamage;
            float r = c.Radius;
            c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this));
            if (c.IsAlive) return;
            // 打死了：带着这具尸体飞回
            corpseDamage = dmg;
            corpseRadius = r;
            corpse = MakeVisual(transform, PrimitiveType.Capsule, new Vector3(r * 2f, r * 2f, r * 2f), new Color(0.42f, 0.36f, 0.34f),
                new Vector3(0f, r - Query.CastHeight + 0.1f, -r));
            corpse.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hitOnReturn.Add(c);
        }

        void StartReturn()
        {
            returning = true;
            var owner = Owner as Combatant;
            lockPoint = Query.AtCastHeight(owner != null ? owner.Position : pos);
            if (corpse != null)
            {
                lockMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(lockMarker.GetComponent<Collider>());
                lockMarker.name = "KnifeReturnPoint";
                lockMarker.transform.position = new Vector3(lockPoint.x, 0.02f, lockPoint.z);
                lockMarker.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
                lockMarker.GetComponent<Renderer>().sharedMaterial = GameManager.Instance.FxMat(new Color(1f, 0.3f, 0.25f, 0.45f));
            }
        }

        void UpdateTransform()
        {
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (lockMarker) Destroy(lockMarker);
        }
    }
}
