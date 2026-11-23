using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// One craft caught in a tachyon field: everything about it runs at the field's time speed (1 normal, 0 stopped),
    /// so it slows to a stop, hangs, and speeds back up smoothly.
    /// - Motion: its velocities scaled from what they were when caught; stopped, its rigidbodies are kinematic.
    /// - Everything its parts run: their modules are switched off while time is stopped (engines burn no fuel,
    ///   converters and containment use nothing, weapons and guidance wait, kerbals on EVA can't move).
    /// - What it looks and sounds like: sounds slowed in pitch then paused, animations (landing gear, kerbals in the
    ///   cockpits and on EVA) slowed and stopped, stock particle effects (plumes, smoke) slowed and stopped, Waterfall
    ///   plumes slowed and frozen in place.
    /// </summary>
    internal sealed class TimeStop
    {
        public readonly Vessel Vessel;
        readonly Dictionary<Part, (Vector3d vel, Vector3 ang, bool wasKinematic)> parts = new Dictionary<Part, (Vector3d, Vector3, bool)>();
        readonly List<(PartModule m, bool wasEnabled, bool wasIsEnabled)> modules = new List<(PartModule, bool, bool)>();
        readonly List<(AudioSource src, float pitch)> sounds = new List<(AudioSource, float)>();
        readonly HashSet<AudioSource> paused = new HashSet<AudioSource>();
        readonly List<(AnimationState state, float speed)> anims = new List<(AnimationState, float)>();
        readonly List<(Animator a, float speed)> animators = new List<(Animator, float)>();
        readonly List<(ParticleSystem ps, float speed)> particles = new List<(ParticleSystem, float)>();
        readonly List<(Behaviour b, bool enabled)> emitters = new List<(Behaviour, bool)>();
        sealed class Scroll { public Material M; public int Id; public float Base, Written; }
        readonly List<Scroll> scrolls = new List<Scroll>();
        double inside;          // the craft's own clock (Unity's shader time units), for Waterfall's scrolling
        bool modulesOff, kinematic;

        static readonly string[] ScrollProps = { "_SpeedX", "_SpeedY", "_Speed", "_DetailSpeedX", "_DetailSpeedY" };
        static readonly HashSet<string> KeepRunning = new HashSet<string> { nameof(ModuleFFWBomb), nameof(ModuleFFWTachyonShield) };

        public static Func<Vector3, Vector3d> RestVelocity;   // (FieldManager's: at rest over the surface)

        public TimeStop(Vessel v, HashSet<Part> taken)
        {
            Vessel = v;
            inside = Time.timeSinceLevelLoad;
            foreach (Part p in v.parts)
            {
                if (p == null || !taken.Add(p)) continue;   // (a piece broken off a caught craft is caught already)
                if (p.rb != null) parts[p] = ((Vector3d)p.rb.velocity - RestVelocity(p.rb.position), p.rb.angularVelocity, p.rb.isKinematic);
                foreach (PartModule m in p.Modules)
                    if (m != null && !KeepRunning.Contains(m.GetType().Name)) modules.Add((m, m.enabled, m.isEnabled));
                Collect(p.transform);
                foreach (ProtoCrewMember c in p.protoModuleCrew)
                    if (c?.KerbalRef != null) Collect(c.KerbalRef.transform);
            }
        }

        public int Parts => parts.Count;

        void Collect(Transform root)
        {
            foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) sounds.Add((a, a.pitch));
            foreach (Animation a in root.GetComponentsInChildren<Animation>(true))
                foreach (AnimationState s in a) anims.Add((s, s.speed));
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) animators.Add((a, a.speed));
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true)) particles.Add((ps, ps.main.simulationSpeed));
            foreach (KSPParticleEmitter e in root.GetComponentsInChildren<KSPParticleEmitter>(true)) emitters.Add((e, e.enabled));
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || !m.shader.name.StartsWith("Waterfall")) continue;
                    foreach (string prop in ScrollProps)
                    {
                        if (!m.HasProperty(prop)) continue;
                        int id = Shader.PropertyToID(prop);
                        float v = m.GetFloat(id);
                        scrolls.Add(new Scroll { M = m, Id = id, Base = v, Written = v });
                    }
                }
        }

        /// <summary>FixedUpdate: motion and modules at this time speed.</summary>
        public void Physics(double speed)
        {
            bool stop = speed < 0.01;
            foreach (var kv in parts)
            {
                Part p = kv.Key;
                if (p == null || p.rb == null || Vessel == null || Vessel.packed) continue;
                if (stop)
                {
                    if (!p.rb.isKinematic) { p.rb.velocity = Vector3.zero; p.rb.angularVelocity = Vector3.zero; p.rb.isKinematic = true; }
                    continue;
                }
                if (kinematic && p.rb.isKinematic && !kv.Value.wasKinematic) p.rb.isKinematic = false;
                if (p.rb.isKinematic) continue;
                p.rb.velocity = RestVelocity(p.rb.position) + kv.Value.vel * speed;
                p.rb.angularVelocity = kv.Value.ang * (float)speed;
            }
            kinematic = stop;
            if (speed < 0.05 && !modulesOff)
            {
                modulesOff = true;
                // (both switches: Unity's stops its FixedUpdate and Update, KSP's its OnFixedUpdate and OnUpdate)
                foreach (var (m, _, _) in modules) if (m != null) { m.enabled = false; m.isEnabled = false; }
            }
            else if (speed > 0.5 && modulesOff)
            {
                modulesOff = false;
                foreach (var (m, was, wasIs) in modules) if (m != null) { m.enabled = was; m.isEnabled = wasIs; }
            }
        }

        /// <summary>LateUpdate: sounds, animations and effects at this time speed.</summary>
        public void Effects(double speed, float dt)
        {
            float s = (float)Math.Max(0, Math.Min(1, speed));
            foreach (var (src, pitch) in sounds)
            {
                if (src == null) continue;
                if (s < 0.02f)
                {
                    if (src.isPlaying) { src.Pause(); paused.Add(src); }
                }
                else
                {
                    if (paused.Remove(src)) src.UnPause();
                    src.pitch = pitch * s;
                }
            }
            foreach (var (state, sp) in anims) if (state != null) state.speed = sp * s;
            foreach (var (a, sp) in animators) if (a != null) a.speed = sp * s;
            foreach (var (ps, sp) in particles) if (ps != null) { var main = ps.main; main.simulationSpeed = sp * s; }
            foreach (var (b, en) in emitters) if (b != null) b.enabled = en && s > 0.05f;
            // Waterfall scrolls its textures by speed x shader time: scaling each speed by (our clock / shader time) puts
            // them where our slower clock says (exactly frozen when it stops). Waterfall may set a speed itself (its
            // controllers): a value we didn't write is its new base.
            inside += dt * s;
            double T = Math.Max(Time.timeSinceLevelLoad, 1e-3);
            float k = (float)(inside / T);
            foreach (Scroll sc in scrolls)
            {
                if (sc.M == null) continue;
                float cur = sc.M.GetFloat(sc.Id);
                if (Mathf.Abs(cur - sc.Written) > 1e-6f) sc.Base = cur;
                sc.Written = sc.Base * k;
                sc.M.SetFloat(sc.Id, sc.Written);
            }
        }

        /// <summary>Everything back to normal, at the speeds it had when caught.</summary>
        public void Release()
        {
            foreach (var kv in parts)
            {
                Part p = kv.Key;
                if (p == null || p.rb == null) continue;
                if (Vessel != null && !Vessel.packed) p.rb.isKinematic = kv.Value.wasKinematic;
                if (!p.rb.isKinematic)
                {
                    p.rb.velocity = RestVelocity(p.rb.position) + kv.Value.vel;
                    p.rb.angularVelocity = kv.Value.ang;
                }
            }
            if (modulesOff) foreach (var (m, was, wasIs) in modules) if (m != null) { m.enabled = was; m.isEnabled = wasIs; }
            modulesOff = false;
            foreach (var (src, pitch) in sounds) if (src != null) { if (paused.Contains(src)) src.UnPause(); src.pitch = pitch; }
            paused.Clear();
            foreach (var (state, sp) in anims) if (state != null) state.speed = sp;
            foreach (var (a, sp) in animators) if (a != null) a.speed = sp;
            foreach (var (ps, sp) in particles) if (ps != null) { var main = ps.main; main.simulationSpeed = sp; }
            foreach (var (b, en) in emitters) if (b != null) b.enabled = en;
            foreach (Scroll sc in scrolls)
                if (sc.M != null && Mathf.Abs(sc.M.GetFloat(sc.Id) - sc.Written) < 1e-6f) sc.M.SetFloat(sc.Id, sc.Base);
        }
    }
}
