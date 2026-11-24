using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Something alive in a model (the Kraken Missile's lump of flesh): <see cref="transformName"/> throbs with a heartbeat
    /// (a double beat every <see cref="period"/> s, swelling by <see cref="scale"/>), and its emissive veins glow dimly in
    /// time with it, brighter once the missile is fired.
    /// </summary>
    public class ModuleFFWPulse : PartModule
    {
        [KSPField] public string transformName = "flesh";
        [KSPField] public float period = 1.5f;
        [KSPField] public float scale = 0.05f;
        [KSPField] public string color = "255, 40, 30, 255";
        [KSPField] public float idleGlow = 0.8f;
        [KSPField] public float firedGlow = 1.6f;

        static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
        Transform beating;
        Vector3 baseScale;
        readonly List<Material> mats = new List<Material>();
        PartModule missile;
        Color colour;
        float offset;

        public override void OnStart(StartState state)
        {
            colour = Core.Rgba.Parse(color, new Core.Rgba(1, 0.15f, 0.1f, 1)).ToColor();
            beating = part.FindModelTransform(transformName);
            if (beating == null) { Log.Warn($"{part.partInfo?.title}: ModuleFFWPulse: no transform {transformName}."); return; }
            baseScale = beating.localScale;
            foreach (Renderer r in beating.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    if (m != null && m.HasProperty(EmissiveId)) mats.Add(m);
            offset = Random.value * period;     // (not every missile on the rail in step)
            if (HighLogic.LoadedSceneIsFlight) { Bda.Init(); missile = Bda.FindMissile(part); }
        }

        /// <summary>Lub-dub: a strong beat, a weaker one just after, then rest (0-1).</summary>
        static float Beat(float phase)
        {
            float a = (phase - 0.08f) / 0.06f, b = (phase - 0.28f) / 0.07f;
            return Mathf.Exp(-a * a) + 0.6f * Mathf.Exp(-b * b);
        }

        void Update()
        {
            if (beating == null) return;
            bool fired = missile != null && Bda.MissileFired(missile);
            float p = fired ? period * 0.5f : period;     // (it gets excited once launched)
            float beat = Beat(Mathf.Repeat(Time.time + offset, p) / p);
            beating.localScale = baseScale * (1 + scale * beat);
            float k = (fired ? firedGlow : idleGlow) * (0.6f + 0.4f * beat);
            Color c = colour * k; c.a = 1;
            foreach (Material m in mats) if (m != null) m.SetColor(EmissiveId, c);
        }

        void OnDestroy() { foreach (Material m in mats) if (m != null) Destroy(m); }
    }
}
