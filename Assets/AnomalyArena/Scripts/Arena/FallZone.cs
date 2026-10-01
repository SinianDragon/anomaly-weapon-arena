using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Trigger placed outside each gap: touching it counts as falling, regardless of distance to the edge.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FallZone : MonoBehaviour
    {
        [Tooltip("Points away from the platform; gives the fall a little outward velocity")] public Vector3 outward = Vector3.forward;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other) => Handle(other);

        // Entering while hooked does not count as falling; if still inside after release, the next frame counts
        void OnTriggerStay(Collider other) => Handle(other);

        void Handle(Collider other)
        {
            var c = Combatant.From(other);
            if (c != null) c.OnEnterFallZone(outward);
        }
    }
}
