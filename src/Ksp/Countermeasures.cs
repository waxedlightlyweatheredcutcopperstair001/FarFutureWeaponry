using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Signal Scrambler: every other craft within <see cref="radius"/> loses its radars and its CommNet link for
    /// <see cref="seconds"/>, and those flown remotely (no Kerbals aboard) can't be controlled meanwhile; the craft running it
    /// can't be followed by CommNet tracking. The screen glitches as it fires.
    /// </summary>
    public class ModuleFFWScrambler : PartModule
    {
        [KSPField] public float radius = 5000;
        [KSPField] public float seconds = 20;
        [KSPField] public float ec = 2000;
        [KSPField] public float cooldown = 60;
        [KSPField(guiActive = true, guiName = "Scrambler")] public string status = "ready";

        float readyAt;

        [KSPEvent(guiActive = true, guiName = "Scramble signals")]
        public void Scramble()
        {
            if (Time.time < readyAt) return;
            if (!ModuleFFWHologram.PayEc(part, ec)) { ScreenMessages.PostScreenMessage($"Scrambler: need {ec:0} ElectricCharge.", 3, ScreenMessageStyle.UPPER_CENTER); return; }
            readyAt = Time.time + cooldown;
            Bda.Init();
            int hit = 0;
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || v == vessel || Vector3.Distance(v.CoM, vessel.CoM) > radius) continue;
                Targets.Blackout(v, seconds);
                Targets.RadarsOff(v, seconds);
                Targets.NoControl(v, seconds);
                hit++;
            }
            Targets.Hide(vessel, seconds);
            if (vessel.isActiveVessel) ScreenGlitch.Play(0.7f, 1);
            Log.Info($"{vessel.vesselName}: signals scrambled within {radius / 1000:0.#} km ({hit} craft) for {seconds:0} s.");
        }

        [KSPAction("Scramble signals")] public void ScrambleAction(KSPActionParam p) => Scramble();

        public override string GetInfo() =>
            $"<b>Scrambler:</b> craft within {radius / 1000:0.#} km lose radar and CommNet for {seconds:0} s; remotely flown ones can't be controlled. Hides this craft from CommNet tracking. {ec:0} EC, {cooldown:0} s cooldown.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            status = Time.time < readyAt ? $"recharging ({readyAt - Time.time:0} s)" : "ready";
        }
    }

    /// <summary>
    /// Alcubierre Micro-Hop Drive: a big burst of ElectricCharge folds space and puts the craft <see cref="distance"/> metres
    /// further along its way (along its velocity; its nose when barely moving), keeping its speed. Every lock on it breaks:
    /// FFW trackers, BDArmory radars and the missiles going for it. Space implodes where it left and where it arrives.
    /// </summary>
    public class ModuleFFWHopDrive : PartModule
    {
        [KSPField] public float distance = 500;
        [KSPField] public float ec = 5000;
        [KSPField] public float cooldown = 45;
        [KSPField(guiActive = true, guiName = "Hop drive")] public string status = "ready";

        float readyAt;

        [KSPEvent(guiActive = true, guiName = "Micro-hop")]
        public void Hop()
        {
            if (Time.time < readyAt || vessel == null || vessel.packed) return;
            Vector3 dir = vessel.srfSpeed > 5 ? ((Vector3)vessel.srf_velocity).normalized : vessel.ReferenceTransform.up;
            Vector3 from = vessel.CoM, to = from + dir * distance;
            // Not into the ground.
            CelestialBody body = vessel.mainBody;
            double lat = body.GetLatitude(to), lon = body.GetLongitude(to), alt = body.GetAltitude(to);
            double ground = body.TerrainAltitude(lat, lon, true);
            if (body.ocean) ground = System.Math.Max(ground, 0);
            if (alt - ground < Mathf.Max(5, vessel.vesselSize.magnitude))
            {
                ScreenMessages.PostScreenMessage("Micro-hop: the way ahead runs into the ground.", 3, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (!ModuleFFWHologram.PayEc(part, ec)) { ScreenMessages.PostScreenMessage($"Micro-hop: need {ec:0} ElectricCharge.", 3, ScreenMessageStyle.UPPER_CENTER); return; }
            readyAt = Time.time + cooldown;
            FieldManager.Instance?.HopFlash(from);
            vessel.SetPosition(vessel.transform.position + dir * distance, false);
            FieldManager.Instance?.HopFlash(to);
            Targets.BreakLocks(vessel);
            Log.Info($"{vessel.vesselName}: micro-hop {distance:0} m.");
        }

        [KSPAction("Micro-hop")] public void HopAction(KSPActionParam p) => Hop();

        public override string GetInfo() =>
            $"<b>Micro-hop:</b> jumps {distance:0} m along the way the craft is going and breaks every lock on it. {ec:0} EC, {cooldown:0} s cooldown.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            status = Time.time < readyAt ? $"recharging ({readyAt - Time.time:0} s)" : "ready";
        }
    }

    /// <summary>The screen glitching for a moment (the scrambler, holograms switching on): torn bars of colour and offset strips.</summary>
    internal sealed class ScreenGlitch : MonoBehaviour
    {
        static ScreenGlitch instance;
        float until, length, strength;

        public static void Play(float seconds, float strength)
        {
            if (instance == null) { var go = new GameObject("FFW screen glitch"); DontDestroyOnLoad(go); instance = go.AddComponent<ScreenGlitch>(); }
            instance.until = Time.realtimeSinceStartup + seconds; instance.length = seconds; instance.strength = strength;
        }

        void OnGUI()
        {
            float left = until - Time.realtimeSinceStartup;
            if (left <= 0 || Event.current.type != EventType.Repaint) return;
            float k = strength * Mathf.Clamp01(left / length * 1.5f);
            int bars = Mathf.CeilToInt(14 * k);
            Color was = GUI.color;
            for (int i = 0; i < bars; i++)
            {
                float y = Random.value * Screen.height, h = Random.Range(2f, 28f) * k;
                float x = Random.Range(-60f, 60f) * k;
                Color c = Random.value < 0.5f ? new Color(0.2f, 1, 0.9f) : Random.value < 0.5f ? new Color(1, 0.2f, 0.6f) : Color.white;
                c.a = Random.Range(0.08f, 0.35f) * k;
                GUI.color = c;
                GUI.DrawTexture(new Rect(x, y, Screen.width, h), Texture2D.whiteTexture);
            }
            GUI.color = was;
        }
    }
}
