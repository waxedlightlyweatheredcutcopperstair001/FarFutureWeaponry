using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// A tachyon weapon's hit (the Tachyon Gun's pulses, the Chronos Rust-Dart): the craft loses speed (along the way it's
    /// moving over the surface, never past a stop), the part rusts, and it takes damage. A craft with an active
    /// Tachyon-be-gone shrugs all of it off.
    /// </summary>
    internal static class TachyonHit
    {
        public static void Apply(Part part, float slowPerHit, float rust, float damage)
        {
            if (part == null || part.vessel == null) return;
            Vessel v = part.vessel;
            if (ModuleFFWTachyonShield.Protects(v)) return;
            if (slowPerHit > 0 && !v.packed)
            {
                Vector3d sv = v.srf_velocity;
                double speed = sv.magnitude;
                if (speed > 1e-3)
                {
                    Vector3 dv = (Vector3)(sv / speed * (speed - Slow.NewSpeed(speed, slowPerHit)));
                    foreach (Part p in v.parts) if (p != null && p.rb != null && !p.rb.isKinematic) p.rb.velocity -= dv;
                }
            }
            if (rust > 0) ModuleFFWRust.AddRust(part, rust);
            if (damage > 0) Bda.AddDamage(part, damage);
        }
    }

    /// <summary>
    /// Rust on a part (every part gets this module, by a ModuleManager patch): a level 0-1 saved with the craft, shown as
    /// the part's colours turning a dull orange-brown and losing their shine.
    /// </summary>
    public class ModuleFFWRust : PartModule
    {
        [KSPField(isPersistant = true)] public float rust = 0;

        readonly List<(Material m, Color colour, float shine, bool hasShine)> originals = new List<(Material, Color, float, bool)>();
        float shown = -1;
        static readonly int ColorId = Shader.PropertyToID("_Color"), ShineId = Shader.PropertyToID("_Shininess");

        public static void AddRust(Part p, float amount)
        {
            var m = p.FindModuleImplementing<ModuleFFWRust>();
            if (m == null) return;
            m.rust = (float)Rust.Add(m.rust, amount);
            m.Show();
        }

        public override void OnStart(StartState state)
        {
            if (rust > 0.001f) Show();
        }

        void Show()
        {
            if (Mathf.Abs(rust - shown) < 0.002f) return;
            if (originals.Count == 0)
            {
                // (the part's own material copies, only once it rusts)
                foreach (Renderer r in part.FindModelComponents<Renderer>())
                {
                    if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                    foreach (Material m in r.materials)
                        if (m != null && m.HasProperty(ColorId))
                            originals.Add((m, m.GetColor(ColorId), m.HasProperty(ShineId) ? m.GetFloat(ShineId) : 0, m.HasProperty(ShineId)));
                }
            }
            shown = rust;
            Rust.Tint(rust, out double tr, out double tg, out double tb);
            float shine = (float)Rust.Shine(rust);
            foreach (var (m, c, s, hasShine) in originals)
            {
                if (m == null) continue;
                m.SetColor(ColorId, new Color(c.r * (float)tr, c.g * (float)tg, c.b * (float)tb, c.a));
                if (hasShine) m.SetFloat(ShineId, s * shine);
            }
        }
    }
}
