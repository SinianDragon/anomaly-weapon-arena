using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Charge Swing routine. Enter: left button pressed. Exit: left button released → swing at the charge ratio; knocked back / hooked / dead → interrupted, no swing.
    /// Movement slows while charging; the sector preview on the ground grows with the charge and blinks when full.
    /// </summary>
    public class SwingCharge : WeaponRuntime
    {
        GunSwingEffect cfg;
        float t;
        Mesh mesh;
        Material mat;
        Transform preview;

        public float Charge01 => cfg ? Mathf.Clamp01(t / Mathf.Max(0.01f, cfg.maxChargeTime)) : 0f;
        public override float SpeedMultiplier => cfg.chargeMoveMultiplier;

        public void Begin(GunSwingEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            preview = new GameObject("ChargePreview").transform;
            preview.SetParent(transform, false);
            mesh = new Mesh();
            preview.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = preview.gameObject.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = GameManager.Instance.FxMat(new Color(1f, 0.8f, 0.3f, 0.25f));
            r.sharedMaterial = mat;
            UpdatePreview();
        }

        void Update()
        {
            if (user == null || !user.IsAlive || user.State != CharacterState.Normal)
            {
                Finish();
                return;
            }
            t += Time.deltaTime;
            UpdatePreview();
        }

        void UpdatePreview()
        {
            float k = Charge01;
            Vector3 p = user.Position;
            preview.position = new Vector3(p.x, 0.04f, p.z);
            preview.rotation = Quaternion.LookRotation(Query.Flat(user.AimDirection));
            Fx.SectorMesh(mesh, Mathf.Lerp(cfg.minRadius, cfg.maxRadius, k), Mathf.Lerp(cfg.minArc, cfg.maxArc, k));
            // Orange-red: readable on both the grey whitebox floor and the yellow sand of illustrated mode
            float a = k >= 1f ? 0.55f + 0.2f * Mathf.Sin(Time.time * 25f) : Mathf.Lerp(0.3f, 0.5f, k);
            mat.color = new Color(1f, 0.35f, 0.1f, a);
        }

        public override void OnUseReleased()
        {
            cfg.Swing(user, Charge01);
            Finish();
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (mat) Destroy(mat);
        }
    }
}
