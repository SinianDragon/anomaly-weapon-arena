using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 刀 · 钩子：原地不动，刀刃向瞄准方向伸长，钩住路上第一个敌人拉到身前当掩体；再按左键扔出去。
    /// 反噬：扔出去的敌人没死，落地后马上回来打你。利用：拿它挡刀；朝墙或缺口扔。
    /// 钩没钩到都扣 1 次；投掷不再扣次。
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Hook")]
    public class KnifeHookEffect : WeaponEffect
    {
        public float range = 10f;
        public float extendSpeed = 40f;
        public float retractSpeed = 60f;
        public float pullSpeed = 25f;
        [Tooltip("拖着敌人移动时的速度倍率")] public float dragSpeedMultiplier = 0.6f;
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
