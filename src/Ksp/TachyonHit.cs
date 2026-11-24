using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// A tachyon weapon's hit (the Tachyon Gun's pulses, the Chronos Rust-Dart). Time on the whole craft slows (like the
    /// Tachyon Bomb, but slowed instead of stopped: its motion, sounds, plumes and animations; Core/Slow's profile) for a
    /// while and then speeds back up; the whole craft rusts (at once per hit, or creeping up while it's slowed); the part
    /// that was hit takes the damage. A craft with an active Tachyon-be-gone shrugs all of it off.
    /// </summary>
    internal static class TachyonHit
    {
        // Parts a tachyon hit just struck, tagged "debris" for VFX Reconfigured: if the hit destroys one it breaks apart
        // (wreckage only) instead of exploding, unless it holds a warhead or an exotic fuel like antimatter. Untagged again
        // if it survives.
        static readonly Dictionary<Part, float> broken = new Dictionary<Part, float>();
        static readonly List<Part> expired = new List<Part>();
        const float DebrisSeconds = 1.5f;

        sealed class Slowed
        {
            public TimeStop Stop;
            public float Start, HoldEnd, Factor, RustRate, Speed = 1;
        }
        static readonly Dictionary<Vessel, Slowed> slowed = new Dictionary<Vessel, Slowed>();
        static readonly List<Vessel> done = new List<Vessel>();

        /// <param name="slowFactor">time speed while slowed (0.9: 10% slower; 1: no slowing)</param>
        /// <param name="slowSeconds">how long it holds (a new hit while slowed extends it)</param>
        /// <param name="rustPerHit">rust added to every part of the craft at once</param>
        /// <param name="rustPerSecond">rust added to every part of the craft each second while it's slowed</param>
        public static void Apply(Part part, float slowFactor, float slowSeconds, float rustPerHit, float rustPerSecond, float damage)
        {
            if (part == null || part.vessel == null) return;
            Vessel v = part.vessel;
            if (ModuleFFWTachyonShield.Protects(v)) return;
            if (damage > 0 && part.FindModuleImplementing<ModuleFFWBomb>() == null)
            {
                if (!broken.ContainsKey(part)) Vfxr.SetPartLook(part, "debris");
                broken[part] = Time.time + DebrisSeconds;
            }
            if (slowFactor < 1 && slowSeconds > 0 && !v.packed) Slow(v, slowFactor, slowSeconds, rustPerSecond);
            if (rustPerHit > 0) foreach (Part p in v.parts) if (p != null) ModuleFFWRust.AddRust(p, rustPerHit);
            if (damage > 0) ModuleFFWForceField.Unshielded(() => Bda.AddDamage(part, damage));
        }

        static void Slow(Vessel v, float factor, float seconds, float rustPerSecond)
        {
            float now = Time.time;
            if (slowed.TryGetValue(v, out Slowed sl))
            {
                sl.HoldEnd = Mathf.Max(sl.HoldEnd, now + seconds);
                sl.Factor = Mathf.Min(sl.Factor, factor);
                sl.RustRate = Mathf.Max(sl.RustRate, rustPerSecond);
                return;
            }
            if (TimeStop.Held(v)) return;      // (frozen by a tachyon field: that hold comes first)
            var stop = new TimeStop(v, new HashSet<Part>());
            if (stop.Parts == 0) return;
            slowed[v] = new Slowed { Stop = stop, Start = now, HoldEnd = now + seconds, Factor = factor, RustRate = rustPerSecond };
            Log.Info($"{v.vesselName}: tachyon decay, time at {factor * 100:0}% for {seconds:0.#} s.");
        }

        public static bool IsSlowed(Vessel v) => v != null && slowed.ContainsKey(v);

        /// <summary>Let a craft go at once (a Tachyon Bomb's field catching it takes over).</summary>
        public static void Cancel(Vessel v)
        {
            if (v == null || !slowed.TryGetValue(v, out Slowed sl)) return;
            sl.Stop.Release();
            slowed.Remove(v);
        }

        /// <summary>FixedUpdate (FieldManager): motion at each slowed craft's time speed; release when it's back to normal.</summary>
        public static void FixedTick()
        {
            if (slowed.Count == 0) return;
            float now = Time.time;
            done.Clear();
            foreach (var kv in slowed)
            {
                Slowed sl = kv.Value;
                if (kv.Key == null) { done.Add(kv.Key); continue; }
                float t = now - sl.Start;
                if (t >= Core.Slow.End(sl.HoldEnd - sl.Start)) { sl.Stop.Release(); done.Add(kv.Key); continue; }
                sl.Speed = (float)Core.Slow.Profile(t, sl.Factor, sl.HoldEnd - sl.Start);
                sl.Stop.Physics(sl.Speed);
            }
            foreach (Vessel v in done) slowed.Remove(v);
        }

        /// <summary>Each frame (FieldManager): sounds, plumes, animations at the slowed speed; creeping rust; debris tags.</summary>
        public static void Tick()
        {
            float now = Time.time, dt = Time.deltaTime;
            foreach (var kv in slowed)
            {
                Slowed sl = kv.Value;
                if (kv.Key == null) continue;
                sl.Stop.Effects(sl.Speed, dt);
                if (sl.RustRate > 0 && now < sl.HoldEnd)
                    foreach (Part p in kv.Key.parts) if (p != null) ModuleFFWRust.AddRustLinear(p, sl.RustRate * dt);
            }
            if (broken.Count == 0) return;
            expired.Clear();
            foreach (var kv in broken) if (kv.Key == null || now > kv.Value) expired.Add(kv.Key);
            foreach (Part p in expired)
            {
                if (p != null) Vfxr.SetPartLook(p, null);
                broken.Remove(p);
            }
        }

        public static void Clear()
        {
            foreach (var kv in slowed) kv.Value.Stop.Release();
            slowed.Clear();
            broken.Clear();
        }
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

        /// <summary>Add rust in a straight line (creeping rust: small steps every frame).</summary>
        public static void AddRustLinear(Part p, float amount)
        {
            var m = p.FindModuleImplementing<ModuleFFWRust>();
            if (m == null || amount <= 0) return;
            m.rust = Mathf.Clamp01(m.rust + amount);
            m.Show();
        }

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

        bool pending;
        const int OverlaysPerFrame = 25;
        static int budgetFrame = -1, budgetUsed;

        static bool TakeBudget()
        {
            if (Time.frameCount != budgetFrame) { budgetFrame = Time.frameCount; budgetUsed = 0; }
            return budgetUsed++ < OverlaysPerFrame;
        }

        void Update() { if (pending) Show(); }

        void Show()
        {
            if (Mathf.Abs(rust - shown) < 0.002f) return;
            shown = rust;
            FfwShaders.Load();
            if (FfwShaders.Rust != null)
            {
                if (overlay == null)
                {
                    // (a hit rusts the whole craft: its parts' overlays are built a few dozen a frame, not all at once)
                    if (!TakeBudget()) { shown = -1; pending = true; return; }
                    pending = false;
                    overlay = new PartOverlay(part, FfwShaders.Rust, "FFW rust");
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
