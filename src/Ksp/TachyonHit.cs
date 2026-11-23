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
        // Parts a tachyon hit just struck, tagged "debris" for VFX Reconfigured: if the hit destroys one it breaks apart
        // (wreckage only) instead of exploding, unless it holds a warhead or an exotic fuel like antimatter. Untagged again
        // if it survives.
        static readonly Dictionary<Part, float> broken = new Dictionary<Part, float>();
        static readonly List<Part> expired = new List<Part>();
        const float DebrisSeconds = 1.5f;

        public static void Apply(Part part, float slowPerHit, float rust, float damage)
        {
            if (part == null || part.vessel == null) return;
            Vessel v = part.vessel;
            if (ModuleFFWTachyonShield.Protects(v)) return;
            if (damage > 0 && part.FindModuleImplementing<ModuleFFWBomb>() == null)
            {
                if (!broken.ContainsKey(part)) Vfxr.SetPartLook(part, "debris");
                broken[part] = Time.time + DebrisSeconds;
            }
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

        /// <summary>Each frame (FieldManager): parts that survived their hit lose the "debris" tag.</summary>
        public static void Tick()
        {
            if (broken.Count == 0) return;
            expired.Clear();
            float now = Time.time;
            foreach (var kv in broken) if (kv.Key == null || now > kv.Value) expired.Add(kv.Key);
            foreach (Part p in expired)
            {
                if (p != null) Vfxr.SetPartLook(p, null);
                broken.Remove(p);
            }
        }

        public static void Clear() => broken.Clear();
    }

    /// <summary>
    /// Rust on a part (every part gets this module, by a ModuleManager patch): a level 0-1 saved with the craft. With the
    /// plugin's shaders: patches of rust drawn over the part (FarFutureWeaponry/RustOverlay), spreading as the level
    /// rises. Without them: the part's colours turned evenly towards orange-brown, less shiny.
    /// </summary>
    public class ModuleFFWRust : PartModule
    {
        [KSPField(isPersistant = true)] public float rust = 0;

        PartOverlay overlay;
        readonly List<(Material m, Color colour, float shine, bool hasShine)> originals = new List<(Material, Color, float, bool)>();
        float shown = -1;
        static readonly int ColorId = Shader.PropertyToID("_Color"), ShineId = Shader.PropertyToID("_Shininess"),
            RustId = Shader.PropertyToID("_Rust"), ScaleId = Shader.PropertyToID("_Scale"), SeedId = Shader.PropertyToID("_Seed");

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
            shown = rust;
            FfwShaders.Load();
            if (FfwShaders.Rust != null)
            {
                if (overlay == null)
                {
                    overlay = new PartOverlay(part, FfwShaders.Rust, "FFW rust");
                    Log.Info($"{part.partInfo.title} on {vessel?.vesselName}: rusting ({(overlay.Empty ? "no meshes to draw it on" : "shader")}).");
                    overlay.Block.SetVector(ScaleId, new Vector4(1, 1, 1, 0.4f));
                    overlay.Block.SetFloat(SeedId, (part.persistentId % 997) * 0.37f);
                }
                overlay.Block.SetFloat(RustId, rust);
                overlay.Apply();
                return;
            }
            if (originals.Count == 0)
            {
                Log.Info($"{part.partInfo.title} on {vessel?.vesselName}: rusting (no rust shader: colour tint).");
                foreach (Renderer r in part.FindModelComponents<Renderer>())
                {
                    if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                    foreach (Material m in r.materials)
                        if (m != null && m.HasProperty(ColorId))
                            originals.Add((m, m.GetColor(ColorId), m.HasProperty(ShineId) ? m.GetFloat(ShineId) : 0, m.HasProperty(ShineId)));
                }
            }
            Rust.Tint(rust, out double tr, out double tg, out double tb);
            float shine = (float)Rust.Shine(rust);
            foreach (var (m, c, s, hasShine) in originals)
            {
                if (m == null) continue;
                m.SetColor(ColorId, new Color(c.r * (float)tr, c.g * (float)tg, c.b * (float)tb, c.a));
                if (hasShine) m.SetFloat(ShineId, s * shine);
            }
        }

        void OnDestroy()
        {
            overlay?.Destroy();
        }
    }
}
