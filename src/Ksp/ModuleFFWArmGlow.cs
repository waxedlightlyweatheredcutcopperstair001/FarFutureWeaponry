using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Lights a model's emissive parts once the weapon is released and armed: the part's KSP/Emissive materials get
    /// _EmissiveColor raised from black to <see cref="color"/> over <see cref="rampSeconds"/>, <see cref="armDelay"/> s after
    /// BDArmory fires it, then pulse gently. The emissive texture decides what glows (the warp bomb's trench).
    /// </summary>
    public class ModuleFFWArmGlow : PartModule
    {
        [KSPField] public string color = "90, 165, 255, 255";
        [KSPField] public float armDelay = 1;
        [KSPField] public float rampSeconds = 0.6f;
        [KSPField] public float pulseRate = 0.8f;      // Hz
        [KSPField] public float pulseDepth = 0.25f;
        [KSPField] public float idleGlow = 0;          // glow before it's armed (0: dark), as a share of the colour

        static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
        readonly List<Material> mats = new List<Material>();
        readonly List<Material> instances = new List<Material>();     // (every copy r.materials made: all destroyed)
        PartModule missile;
        float firedAt = -1;
        Color colour;

        public override void OnStart(StartState state)
        {
            colour = Core.Rgba.Parse(color, new Core.Rgba(0.35f, 0.65f, 1, 1)).ToColor();
            foreach (Renderer r in part.FindModelComponents<Renderer>())
                foreach (Material m in Instanced(r))       // (instances: only this part glows)
                    if (m != null && m.HasProperty(EmissiveId)) { mats.Add(m); }
            SetGlow(idleGlow);
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || mats.Count == 0) return;
            if (firedAt < 0) { if (Bda.MissileFired(missile)) firedAt = Time.time; else return; }
            float t = Time.time - firedAt - armDelay;
            if (t < 0) return;
            float full = 1 - pulseDepth + pulseDepth * Mathf.Sin(2 * Mathf.PI * pulseRate * t);
            SetGlow(Mathf.Lerp(idleGlow, full, Mathf.Clamp01(t / Mathf.Max(0.01f, rampSeconds))));
        }

        void SetGlow(float k)
        {
            Color c = colour * k; c.a = 1;
            foreach (Material m in mats) if (m != null) m.SetColor(EmissiveId, c);
        }

        IEnumerable<Material> Instanced(Renderer r) { Material[] ms = r.materials; instances.AddRange(ms); return ms; }

        void OnDestroy() { foreach (Material m in instances) if (m != null) Destroy(m); }
    }
}
