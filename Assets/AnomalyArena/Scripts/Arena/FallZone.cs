using UnityEngine;

namespace AnomalyArena
{
    /// <summary>放在每个缺口外面的触发器：碰到就判定掉下去，不看离边缘多近。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FallZone : MonoBehaviour
    {
        [Tooltip("指向平台外侧，用来给下落一点向外的速度")] public Vector3 outward = Vector3.forward;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other) => Handle(other);

        // 被钩住时进入不会掉；松开后如果还在里面，下一帧照样判定
        void OnTriggerStay(Collider other) => Handle(other);

        void Handle(Collider other)
        {
            var c = Combatant.From(other);
            if (c != null) c.OnEnterFallZone(outward);
        }
    }
}
