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

        /// <summary>
        /// A part's motion in its own time: U is the velocity it has by its own clock (over the surface); its real velocity
        /// is U x the time speed. Each step the forces it felt (gravity, thrust, lift: the change since the last step) are
        /// added to U scaled by the time speed, so a slowed craft still falls, steers and burns, just slower.
        /// </summary>
        sealed class Motion { public Vector3d U, LastRel; public Vector3 Ang, LastAng; public bool WasKinematic, Set; }
        readonly Dictionary<Part, Motion> parts = new Dictionary<Part, Motion>();

        // ---- One hold per craft, and saves that keep modules' own state ----

        /// <summary>The craft held now (frozen or slowed), by whom: a second hold would take the first's state as normal.</summary>
        static readonly Dictionary<Vessel, TimeStop> holding = new Dictionary<Vessel, TimeStop>();
        public static bool Held(Vessel v) => v != null && holding.TryGetValue(v, out var t) && t != null && !t.released;
        bool released;

        /// <summary>
        /// Modules FFW has switched off for a while (a freeze, an antenna blackout), with their own isEnabled. KSP saves
        /// isEnabled (PartModule.Save); a prefix on it writes the module's own value instead (InstallSavePatch).
        /// </summary>
        internal static readonly Dictionary<PartModule, bool> Suppressed = new Dictionary<PartModule, bool>();

        static bool SavePrefix(PartModule __instance, out bool __state)
        {
            __state = false;
            if (__instance != null && Suppressed.TryGetValue(__instance, out bool own) && __instance.isEnabled != own)
            { __state = true; __instance.isEnabled = own; }
            return true;
        }

        static void SavePostfix(PartModule __instance, bool __state) { if (__state && __instance != null) __instance.isEnabled = !__instance.isEnabled; }

        static bool savePatched;
        internal static void InstallSavePatch()
        {
            if (savePatched) return;
            savePatched = true;
            MethodInfo save = typeof(PartModule).GetMethod("Save", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(ConfigNode) }, null);
            if (!HarmonyBridge.Patch(save, typeof(TimeStop).GetMethod(nameof(SavePrefix), BindingFlags.Static | BindingFlags.NonPublic),
                                     typeof(TimeStop).GetMethod(nameof(SavePostfix), BindingFlags.Static | BindingFlags.NonPublic)))
                Log.Warn("Couldn't patch PartModule.Save: saving while a craft is frozen or blacked out keeps its modules switched off.");
        }

        public static void ClearAll() { holding.Clear(); Suppressed.Clear(); }
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
            if (v != null) holding[v] = this;
            inside = Time.timeSinceLevelLoad;
            foreach (Part p in v.parts)
            {
                if (p == null || !taken.Add(p)) continue;   // (a piece broken off a caught craft is caught already)
                if (p.rb != null) parts[p] = new Motion { U = (Vector3d)p.rb.velocity - RestVelocity(p.rb.position), Ang = p.rb.angularVelocity, WasKinematic = p.rb.isKinematic };
                foreach (PartModule m in p.Modules)
                    if (m != null && !KeepRunning.Contains(m.GetType().Name)) modules.Add((m, m.enabled, m.isEnabled));
                Collect(p.transform, p);
                foreach (ProtoCrewMember c in p.protoModuleCrew)
                    if (c?.KerbalRef != null) Collect(c.KerbalRef.transform, null);
            }
        }

        public int Parts => parts.Count;

        readonly HashSet<(Material, int)> scrollSeen = new HashSet<(Material, int)>();

        /// <summary>
        /// Gather what a part runs. Its children include the parts attached below it (and, on the root part, the flight
        /// camera): only what belongs to this part itself (owner; null for a kerbal's model) and isn't a camera's.
        /// </summary>
        void Collect(Transform root, Part owner)
        {
            bool Mine(Component c) => c != null && !Visuals.OnCamera(c) && (owner == null || c.GetComponentInParent<Part>() == owner);
            foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) if (Mine(a)) sounds.Add((a, a.pitch));
            foreach (Animation a in root.GetComponentsInChildren<Animation>(true))
                if (Mine(a)) foreach (AnimationState s in a) anims.Add((s, s.speed));
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) if (Mine(a)) animators.Add((a, a.speed));
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true)) if (Mine(ps)) particles.Add((ps, ps.main.simulationSpeed));
            foreach (KSPParticleEmitter e in root.GetComponentsInChildren<KSPParticleEmitter>(true)) if (Mine(e)) emitters.Add((e, e.enabled));
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (Mine(r)) foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || !m.shader.name.StartsWith("Waterfall")) continue;
                    foreach (string prop in ScrollProps)
                    {
                        if (!m.HasProperty(prop)) continue;
                        int id = Shader.PropertyToID(prop);
                        if (!scrollSeen.Add((m, id))) continue;     // (one entry per material: two would fight)
                        float v = m.GetFloat(id);
                        scrolls.Add(new Scroll { M = m, Id = id, Base = v, Written = v });
                    }
                }
        }

        /// <summary>FixedUpdate: motion and modules at this time speed.</summary>
        public void Physics(double speed)
        {
            bool stop = speed < 0.01;
            float dt = TimeWarp.fixedDeltaTime;
            foreach (var kv in parts)
            {
                Part p = kv.Key; Motion st = kv.Value;
                if (p == null || p.rb == null || Vessel == null || Vessel.packed) continue;
                Vector3d rest = RestVelocity(p.rb.position);
                if (stop)
                {
                    if (!p.rb.isKinematic) { p.rb.velocity = Vector3.zero; p.rb.angularVelocity = Vector3.zero; p.rb.isKinematic = true; }
                    // Held at rest over the surface: a kinematic body stays put in Unity's space, which moves over the
                    // ground at speed (Krakensbane) and in orbit.
                    if (!st.WasKinematic && rest.sqrMagnitude > 1e-4) p.rb.MovePosition(p.rb.position + (Vector3)(rest * dt));
                    st.Set = false;
                    continue;
                }
                if (kinematic && p.rb.isKinematic && !st.WasKinematic) p.rb.isKinematic = false;
                if (p.rb.isKinematic) continue;
                // What the forces did since the last step, by its own (slower) clock.
                if (st.Set)
                {
                    st.U += ((Vector3d)p.rb.velocity - rest - st.LastRel) * speed;
                    st.Ang += (p.rb.angularVelocity - st.LastAng) * (float)speed;
                }
                st.LastRel = st.U * speed;
                st.LastAng = st.Ang * (float)speed;
                p.rb.velocity = rest + st.LastRel;
                p.rb.angularVelocity = st.LastAng;
                st.Set = true;
            }
            kinematic = stop;
            if (speed < 0.05 && !modulesOff)
            {
                modulesOff = true;
                // (both switches: Unity's stops its FixedUpdate and Update, KSP's its OnFixedUpdate and OnUpdate)
                foreach (var (m, _, wasIs) in modules) if (m != null) { Suppressed[m] = wasIs; m.enabled = false; m.isEnabled = false; }
            }
            else if (speed > 0.5 && modulesOff)
            {
                modulesOff = false;
                foreach (var (m, was, wasIs) in modules) if (m != null) { m.enabled = was; m.isEnabled = wasIs; Suppressed.Remove(m); }
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
            released = true;
            if (Vessel != null && holding.TryGetValue(Vessel, out var h) && h == this) holding.Remove(Vessel);
            foreach (var kv in parts)
            {
                Part p = kv.Key;
                if (p == null || p.rb == null) continue;
                if (Vessel != null && !Vessel.packed) p.rb.isKinematic = kv.Value.WasKinematic;
                if (!p.rb.isKinematic)
                {
                    p.rb.velocity = RestVelocity(p.rb.position) + kv.Value.U;
                    p.rb.angularVelocity = kv.Value.Ang;
                }
            }
            if (modulesOff) foreach (var (m, was, wasIs) in modules) if (m != null) { m.enabled = was; m.isEnabled = wasIs; Suppressed.Remove(m); }
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
