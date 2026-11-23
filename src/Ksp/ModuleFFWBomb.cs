using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// What happens when a BDArmory bomb carrying this module goes off, on top of (or instead of) its warhead:
    /// - look: how VFX Reconfigured draws the warhead's explosion ("antimatter", "mushroom", or "none" when the field
    ///   below is the whole effect);
    /// - effect: a field at the detonation point ("gravity": pull, then a blast; "warp": a black hole that pulls and
    ///   destroys; "tachyon": a sphere that freezes every craft inside), run by FieldManager;
    /// - a flash (flashColor, flashRadius, flashSeconds);
    /// - shatter: the bomb breaks into fragments that hang in its own field and reassemble into a harmless dud.
    ///
    /// The bomb's BDArmory warhead (BDExplosivePart or BDModuleNuke) decides when: this fires only if it really
    /// detonated (armed and set off), not when an unarmed bomb is shot apart or the scene changes.
    /// </summary>
    public class ModuleFFWBomb : PartModule
    {
        [KSPField] public string look = "";
        [KSPField] public string effect = "none";
        [KSPField] public float radius = 300;
        [KSPField] public float innerRadius = 0;
        [KSPField] public float accelG = 6;
        [KSPField] public float duration = 3;
        [KSPField] public float blastTnt = 0;
        [KSPField] public string blastModelPath = "FarFutureWeaponry/FX/Hits/gravityBlast";
        [KSPField] public string blastSoundPath = "FarFutureWeaponry/Sounds/explode1";
        [KSPField] public string flashColor = "";
        [KSPField] public float flashRadius = 0;
        [KSPField] public float flashSeconds = 1.5f;
        [KSPField] public bool shatter = false;

        // Telemetry Leech: antennas off and ElectricCharge drained on craft within empRadius, with arcs.
        [KSPField] public float empRadius = 0;
        [KSPField] public float antennaSeconds = 30;
        [KSPField] public float ecDrain = 0;
        [KSPField] public string arcColor = "100, 230, 255, 255";
        // Kraken: every Kerbal aboard craft within killCrewRadius dies; a wave of red light passes over the craft.
        [KSPField] public float killCrewRadius = 0;
        [KSPField] public string waveColor = "255, 30, 30, 255";
        // Chronos Rust-Dart: the nearest part within hitRadius rusts, and its craft slows.
        [KSPField] public float hitRadius = 0;
        [KSPField] public float slowPerHit = 0;
        [KSPField] public float rustAmount = 0;

        // The tachyon field's length, set in the part's window (5 to 60 s).
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Freeze time", guiUnits = " s"),
         UI_FloatRange(minValue = 5, maxValue = 60, stepIncrement = 1, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float freezeSeconds = 10;

        bool fired, subscribed;

        public override void OnStart(StartState state)
        {
            bool tachyon = FieldSpec.Parse(effect) == FieldKind.Tachyon;
            Fields[nameof(freezeSeconds)].guiActive = tachyon;
            Fields[nameof(freezeSeconds)].guiActiveEditor = tachyon;
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            Vfxr.Init();
            if (!string.IsNullOrEmpty(look)) Vfxr.SetPartLook(part, look);
            FieldManager.Prepare(Spec());   // (sounds made now, not as it goes off)
            GameEvents.onPartDie.Add(OnPartDie);
            subscribed = true;
        }

        public override string GetInfo()
        {
            var spec = Spec();
            switch (spec.Kind)
            {
                case FieldKind.Gravity:
                    return $"<b>Gravity well:</b> pulls everything within {radius:0} m at {accelG:0.#} g for {duration:0.#} s, then a {blastTnt:0} kg TNT blast.";
                case FieldKind.Warp:
                    return $"<b>Black hole:</b> pulls everything within {radius:0} m at {accelG:0.#} g for {duration:0.#} s and destroys anything within {innerRadius:0} m.";
                case FieldKind.Tachyon:
                    return $"<b>Tachyon field:</b> time stops for every craft within {radius:0} m for {freezeSeconds:0} s (5 to 60, set in the part's window), then gives each its speed back. Craft with an active Tachyon-be-gone are immune.";
            }
            return "";
        }

        FieldSpec Spec() => new FieldSpec
        {
            Kind = FieldSpec.Parse(effect), Radius = radius, InnerRadius = innerRadius, AccelG = accelG,
            Duration = FieldSpec.Parse(effect) == FieldKind.Tachyon ? Mathf.Clamp(freezeSeconds, 5, 60) : duration, BlastTntKg = blastTnt
        };

        void OnPartDie(Part p)
        {
            if (p == part) Fire();
        }

        void Fire()
        {
            if (fired || part == null || !HighLogic.LoadedSceneIsFlight || FieldManager.SceneChanging) return;
            if (!Bda.Detonated(part)) return;
            fired = true;
            Vector3 where = part.transform.position;
            string source = vessel != null ? vessel.vesselName : part.partInfo.title;
            Log.Info($"{part.partInfo.title} detonated: effect {effect}, look '{look}'.");
            FieldManager.Instance?.Detonation(this, Spec(), where, source);
        }

        void OnDestroy()
        {
            if (subscribed) { GameEvents.onPartDie.Remove(OnPartDie); subscribed = false; }
            Fire();   // (destroyed without dying, e.g. Part.Destroy alone: still caught if its warhead went off)
        }
    }

    /// <summary>Tachyon-be-gone: while on, the craft is immune to tachyon weapons (not frozen, not slowed).</summary>
    public class ModuleFFWTachyonShield : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tachyon shield"),
         UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public bool shieldOn = true;

        [KSPAction("Toggle tachyon shield")]
        public void ToggleAction(KSPActionParam param) => shieldOn = !shieldOn;

        public override string GetInfo() => "While on, the craft is not frozen or slowed by tachyon weapons. No power needed.";

        public static bool Protects(Vessel v)
        {
            if (v == null) return false;
            for (int i = 0; i < v.parts.Count; i++)
            {
                var m = v.parts[i].FindModuleImplementing<ModuleFFWTachyonShield>();
                if (m != null && m.shieldOn) return true;
            }
            return false;
        }
    }
}
