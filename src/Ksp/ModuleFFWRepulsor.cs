using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The Gravitolium Repulsor Beam: a BDArmory impulse beam (it pushes the part it hits; very little damage), with a
    /// Push/Pull switch in the part's window that flips the impulse's sign, and space bent along the beam while it fires
    /// (a string of small gravitational lenses from the muzzle to where it hits, through VFX Reconfigured).
    /// </summary>
    public class ModuleFFWRepulsor : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Beam"),
         UI_Toggle(disabledText = "Push", enabledText = "Pull", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public bool pull = false;

        [KSPField] public int lenses = 5;            // along the beam
        [KSPField] public float lensRadius = 7;      // m
        [KSPField] public float lensStrength = 0.35f;

        PartModule weapon;
        float magnitude;

        [KSPAction("Toggle push/pull")]
        public void ToggleAction(KSPActionParam param) => pull = !pull;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            Vfxr.Init();
            weapon = Bda.FindWeapon(part);
            magnitude = Mathf.Abs(Bda.GetImpulse(weapon));
        }

        public override string GetInfo() => "<b>Repulsor:</b> pushes or pulls (switch it in the part's window) whatever the beam holds, hard enough to spin a craft out of control or tear wings off.";

        public void FixedUpdate()
        {
            if (weapon == null) return;
            float want = pull ? -magnitude : magnitude;
            if (Bda.GetImpulse(weapon) != want) Bda.SetImpulse(weapon, want);
        }

        void LateUpdate()
        {
            if (weapon == null || !Bda.LaserBeam(weapon, out Vector3 from, out Vector3 to)) return;
            float len = Vector3.Distance(from, to);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / (lensRadius * 4)), 1, lenses);
            for (int i = 0; i < n; i++)
            {
                float f = (i + 0.5f) / n;
                float wobble = 0.85f + 0.15f * Mathf.Sin(Time.time * 9 + i * 1.7f);
                Vfxr.LensCore(Vector3.Lerp(from, to, f), lensRadius, lensStrength * wobble, 0, 0, 0);
            }
        }
    }
}
