using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Knife · Hook: the user stays in place, the blade extends along the aim direction, hooks the first enemy on the way and pulls it in front as cover; left click again to throw it.
    /// Backfire: the thrown enemy is not dead and comes straight back at you after landing. Exploit: use it to block hits; throw it at a wall or a gap.
    /// One use is spent whether or not the hook connects; throwing does not spend another.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Hook")]
    public class KnifeHookEffect : WeaponEffect
    {
        public float range = 10f;
        public float extendSpeed = 40f;
        public float retractSpeed = 60f;
        public float pullSpeed = 25f;
        [Tooltip("Movement speed multiplier while dragging an enemy")] public float dragSpeedMultiplier = 0.6f;
        public float throwDistance = 8f;
        public float hookRadius = 0.15f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Hook";
            go.GetComponent<Renderer>().sharedMaterial = GameManager.Instance.Mat(new Color(0.82f, 0.88f, 0.94f));
            go.AddComponent<Hook>().Begin(this, weapon, user);
        }
    }
}
