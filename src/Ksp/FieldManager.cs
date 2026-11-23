using System.Collections.Generic;
using System.Reflection;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Runs the bombs' fields after they go off (ModuleFFWBomb): the pull, the black hole's destruction, the tachyon
    /// freeze, the pulse at the end of a gravity well, and how they look and sound (timelines in Core/Fields).
    /// Lenses and flashes are drawn by VFX Reconfigured's EffectsApi; spheres, particles and sounds by the plugin.
    /// Positions are kept in the main body's frame (floating origin).
    ///
    /// Nothing heavy happens at the moment of detonation: sounds are made when the bomb loads (Prepare), and lights are
    /// only used when VFX Reconfigured can't draw the flashes (a light with a range of hundreds of metres makes every
    /// forward-rendered object in reach draw again: a visible hitch).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }
        public static bool SceneChanging { get; private set; }

        const int Rate = 44100;
        const float FallbackLightRange = 250;

        // ---- Sounds, made once per bomb type when it loads ----

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        static string Key(string what, double seconds) => what + ":" + seconds.ToString("0.00");

        static AudioClip Clip(string key, System.Func<float[]> make)
        {
            if (clips.TryGetValue(key, out AudioClip c) && c != null) return c;
            float[] samples = make();
            c = AudioClip.Create("FFW " + key, samples.Length, 1, Rate, false);
            c.SetData(samples, 0);
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clips[key] = c;
            return c;
        }

        static AudioClip StartSound(FieldSpec s)
        {
            switch (s.Kind)
            {
                case FieldKind.Tachyon: return Clip("tachyon-start", () => FieldSound.Sweep(Rate, 0.9f, 1400, 140, 3));
                case FieldKind.Rinse: return Clip("rinse", () => FieldSound.Sweep(Rate, 2.2f, 700, 90, 6));
                case FieldKind.Seismic: return Clip("seismic", () => FieldSound.Seismic(Rate, 4, 7));
                default: return Clip(Key("rumble", s.Duration), () => FieldSound.Rumble(Rate, (float)s.Duration + 0.3f, true, 5));
            }
        }

        const float EndSweepSeconds = 0.7f;
        static AudioClip EndSound() => Clip("tachyon-end", () => FieldSound.Sweep(Rate, EndSweepSeconds, 140, 1500, 4));

        /// <summary>Make a bomb's sounds ahead of time (ModuleFFWBomb.OnStart), so its detonation doesn't stall a frame.</summary>
        internal static void Prepare(FieldSpec s)
        {
            if (s.Kind == FieldKind.None) return;
            StartSound(s);
            if (s.Kind == FieldKind.Tachyon) EndSound();
        }

        // ---- State ----

        sealed class Field
        {
            public FieldSpec Spec;
            public Vector3d Where;
            public float Start;
            public string Source, Weapon, BlastModel, BlastSound;
            public bool Blasted, EndSound;
            public Color Edge, Core, SphereColour;
            public GameObject Go;
            public Light Light;
            public AudioSource Audio;
            public Transform SphereT;
            public Mesh Sphere;
            public Material SphereMat;
            public Color[] SphereColours;
            public Vector3[] SphereNormals;
            public ParticleSystem Air, Shimmer, NearShimmer;
            public readonly List<Material> Materials = new List<Material>();
            public List<DestructibleBuilding> Buildings;
            public readonly List<TimeStop> Caught = new List<TimeStop>();
            public readonly HashSet<Vessel> CaughtVessels = new HashSet<Vessel>();
            public readonly HashSet<Part> CaughtParts = new HashSet<Part>();   // (a piece breaking off a caught craft is caught already)
            public readonly List<(Behaviour fx, PropertyInfo start, float at)> PausedBlasts = new List<(Behaviour, PropertyInfo, float)>();
            public ShatterFx Shatter;
            public bool Removed;
            public readonly HashSet<Vessel> Rinsed = new HashSet<Vessel>();     // Blue Rinse: crews already reached
            public readonly HashSet<Part> RingHit = new HashSet<Part>();        // seismic charge: parts already cut
            public float RingDamage, RingThickness, LastRing;
            public Mesh RingMesh; public Material RingMat; public Transform RingT;
        }

        sealed class Flash
        {
            public Vector3d Where;
            public float Start, Seconds, Radius;
            public Color Edge, Core;
            public Light Light;
        }

        readonly List<Field> fields = new List<Field>();
        readonly List<Flash> flashes = new List<Flash>();

        sealed class Ripple { public Vector3d Where; public float Start, Seconds, Radius; }
        readonly List<Ripple> ripples = new List<Ripple>();

        sealed class Wave
        {
            public Vector3d Where; public float Start, Seconds, Radius; public Color Colour;
            public List<PartOverlay> Overlays;   // with the plugin's shaders: the wave drawn over the craft's own parts
        }
        static readonly int WaveId = Shader.PropertyToID("_Wave"), WaveShapeId = Shader.PropertyToID("_WaveShape"), WaveColourId = Shader.PropertyToID("_Color");
        readonly List<Wave> waves = new List<Wave>();

        sealed class Arcs { public Vector3d Where; public float Start, Seconds, Radius; public LineRenderer[] Lines; public Material Mat; }
        readonly List<Arcs> arcs = new List<Arcs>();
        readonly List<ShatterFx> duds = new List<ShatterFx>();   // tachyon bombs back together, lying where they fell
        readonly List<(Behaviour fx, PropertyInfo start)> live = new List<(Behaviour, PropertyInfo)>();
        readonly List<Part> doomed = new List<Part>();

        void Awake()
        {
            Instance = this;
            SceneChanging = false;
            GameEvents.onGameSceneSwitchRequested.Add(OnSceneSwitch);
        }

        void Start()
        {
            Bda.Init();
            Vfxr.Init();
            TimeStop.RestVelocity = p => RestVelocity(p);
        }

        void OnSceneSwitch(GameEvents.FromToAction<GameScenes, GameScenes> fromTo) => SceneChanging = true;

        // ---- Starting ----

        // ---- Small effects other modules ask for ----

        /// <summary>A short flash (VFX Reconfigured's glow; a small light without it).</summary>
        internal void SmallFlash(Vector3 at, Color c, float radius, float seconds)
        {
            flashes.Add(new Flash
            {
                Where = Visuals.ToFrame(at), Start = Time.time, Seconds = Mathf.Max(0.05f, seconds), Radius = radius,
                Edge = c, Core = Color.Lerp(c, Color.white, 0.7f), Light = Vfxr.Present ? null : MakeLight(at, c)
            });
        }

        /// <summary>
        /// The phase-shift missile vanishing or reappearing: a cyan flash and, with the plugin's shaders, the phase lens
        /// (bent space, the colours torn apart); without them, a ripple of bent space through VFX Reconfigured.
        /// </summary>
        internal void PhaseFlash(Vector3 at, Color c)
        {
            SmallFlash(at, c, 12, 0.35f);
            FfwShaders.Load();
            if (FfwShaders.PhaseLens == null)
            {
                ripples.Add(new Ripple { Where = Visuals.ToFrame(at), Start = Time.time, Seconds = 0.6f, Radius = 30 });
                return;
            }
            if (lensQuad == null)
            {
                lensQuad = new Mesh { name = "FFW phase lens quad" };
                lensQuad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                lensQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                lensQuad.bounds = new Bounds(Vector3.zero, Vector3.one * 2e5f);   // (sized by the shader)
            }
            var go = new GameObject("FFW phase lens");
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = lensQuad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(FfwShaders.PhaseLens);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            lenses.Add(new PhaseLensFx { Where = Visuals.ToFrame(at), Start = Time.time, Go = go, Mat = mr.sharedMaterial });
        }

        /// <summary>
        /// The micro-hop drive folding space: space caving in on a point (a ring of bent space collapsing inward), a flash,
        /// and the phase lens's torn colours.
        /// </summary>
        internal void HopFlash(Vector3 at)
        {
            PhaseFlash(at, new Color(0.7f, 0.75f, 1f));
            ripples.Add(new Ripple { Where = Visuals.ToFrame(at), Start = Time.time, Seconds = 0.5f, Radius = -45 });
        }

        sealed class PhaseLensFx { public Vector3d Where; public float Start; public GameObject Go; public Material Mat; }
        readonly List<PhaseLensFx> lenses = new List<PhaseLensFx>();
        Mesh lensQuad;
        static readonly int PhaseLensId = Shader.PropertyToID("_Lens");
        const float PhaseLensSeconds = 0.7f, PhaseLensRadius = 28;

        // ---- Warheads ----

        internal void Detonation(ModuleFFWBomb bomb, FieldSpec spec, Vector3 where, string source)
        {
            float now = Time.time;
            WarheadEffects(bomb, where);
            if (bomb.flashRadius > 0 && !string.IsNullOrEmpty(bomb.flashColor))
            {
                Color c = Rgba.Parse(bomb.flashColor, new Rgba(1, 1, 1, 1)).ToColor();
                flashes.Add(new Flash
                {
                    Where = Visuals.ToFrame(where), Start = now, Seconds = Mathf.Max(0.2f, bomb.flashSeconds), Radius = bomb.flashRadius,
                    Edge = c, Core = Color.Lerp(c, Color.white, 0.7f), Light = Vfxr.Present ? null : MakeLight(where, c)
                });
            }
            if (spec.Kind == FieldKind.None) return;

            var f = new Field
            {
                Spec = spec, Where = Visuals.ToFrame(where), Start = now, Source = source, Weapon = bomb.part.partInfo.title,
                BlastModel = bomb.blastModelPath, BlastSound = bomb.blastSoundPath
            };
            switch (spec.Kind)
            {
                case FieldKind.Gravity: f.Edge = new Color(0.75f, 0.8f, 1f); f.Core = Color.white; f.SphereColour = new Color(0.85f, 0.9f, 1f); break;
                case FieldKind.Warp: f.Edge = new Color(0.3f, 0.5f, 1f); f.Core = new Color(0.9f, 0.95f, 1f); break;
                case FieldKind.Rinse: f.Edge = new Color(0.25f, 0.55f, 1f); f.Core = new Color(0.8f, 0.9f, 1f); f.SphereColour = new Color(0.3f, 0.6f, 1f); break;
                case FieldKind.Seismic: f.Edge = new Color(0.3f, 0.65f, 1f); f.Core = Color.white; break;
                default: f.Edge = new Color(0.25f, 1f, 0.4f); f.Core = new Color(0.85f, 1f, 0.85f); f.SphereColour = new Color(0.25f, 1f, 0.4f); break;
            }
            f.Go = new GameObject("FFW field " + spec.Kind);
            f.Go.transform.position = where;
            if (!Vfxr.Present) f.Light = MakeLight(where, f.Edge).GetComponent<Light>();
            f.Audio = f.Go.AddComponent<AudioSource>();
            f.Audio.spatialBlend = 1; f.Audio.dopplerLevel = 0; f.Audio.rolloffMode = AudioRolloffMode.Logarithmic;
            f.Audio.minDistance = 30; f.Audio.maxDistance = (float)spec.Radius * 6;
            f.Audio.volume = GameSettings.SHIP_VOLUME;
            f.Audio.PlayOneShot(StartSound(spec));

            if (spec.Kind == FieldKind.Tachyon || spec.Kind == FieldKind.Gravity || spec.Kind == FieldKind.Rinse) MakeSphere(f);
            if (spec.Kind == FieldKind.Seismic) { f.RingDamage = bomb.ringDamage; f.RingThickness = bomb.ringThickness; MakeRing(f); }
            if (spec.Kind == FieldKind.Gravity || spec.Kind == FieldKind.Warp) f.Air = AirParticles(f);
            if (spec.Kind == FieldKind.Tachyon)
            {
                f.Shimmer = ShimmerParticles(f, false);
                f.NearShimmer = ShimmerParticles(f, true);
                if (bomb.shatter) f.Shatter = ShatterFx.Capture(bomb.part);
            }
            if ((spec.Kind == FieldKind.Warp && spec.InnerRadius > 0) || spec.Kind == FieldKind.Seismic)
            {
                f.Buildings = new List<DestructibleBuilding>();
                foreach (DestructibleBuilding b in FindObjectsOfType<DestructibleBuilding>())
                    if (b != null && (b.transform.position - where).sqrMagnitude < (spec.Radius * spec.Radius * 4)) f.Buildings.Add(b);
            }
            fields.Add(f);
            Log.Info($"{spec.Kind} field from {f.Weapon}: radius {spec.Radius:0} m, {spec.Duration:0.#} s" +
                     (Vfxr.Present ? "" : " (no VFX Reconfigured EffectsApi: no lens or flash)"));
        }

        /// <summary>
        /// What a warhead does besides its blast (ModuleFFWBomb):
        /// - Telemetry Leech: craft within empRadius lose their antennas for antennaSeconds and ecDrain ElectricCharge,
        ///   with an electric arc burst;
        /// - Kraken: every Kerbal aboard a craft within killCrewRadius dies, as a wave of red light passes over it;
        /// - Chronos Rust-Dart: parts within hitRadius rust (rustAmount) and their craft slow (slowPerHit).
        /// </summary>
        void WarheadEffects(ModuleFFWBomb b, Vector3 at)
        {
            var near = new List<Vessel>();
            float reach = Mathf.Max(b.empRadius, Mathf.Max(b.killCrewRadius, b.hitRadius));
            if (reach > 0)
                foreach (Vessel v in FlightGlobals.VesselsLoaded)
                    if (v != null && v != b.vessel && ClosestPart(v, at) <= reach) near.Add(v);

            if (b.empRadius > 0)
            {
                foreach (Vessel v in near)
                {
                    if (ClosestPart(v, at) > b.empRadius) continue;
                    if (b.antennaSeconds > 0) Targets.Blackout(v, b.antennaSeconds);
                    if (b.ecDrain > 0)
                    {
                        double drained = 0;
                        foreach (Part p in v.parts)
                        {
                            PartResource r = p.Resources.Get("ElectricCharge");
                            if (r == null || drained >= b.ecDrain) continue;
                            double take = System.Math.Min(r.amount, b.ecDrain - drained);
                            r.amount -= take; drained += take;
                        }
                        Log.Info($"{b.part.partInfo.title}: drained {drained:0} EC from {v.vesselName}.");
                    }
                }
                Color c = Rgba.Parse(b.arcColor, new Rgba(0.4f, 0.9f, 1, 1)).ToColor();
                SmallFlash(at, c, b.empRadius * 0.6f, 0.4f);
                AddArcs(at, b.empRadius, 0.7f, c);
            }

            if (b.killCrewRadius > 0)
            {
                foreach (Vessel v in near)
                {
                    if (ClosestPart(v, at) > b.killCrewRadius) continue;
                    int killed = KillCrew(v);
                    if (killed > 0) Log.Info($"{b.part.partInfo.title}: {killed} Kerbal(s) aboard {v.vesselName} killed.");
                    ContourWave(v, at, Rgba.Parse(b.waveColor, new Rgba(1, 0.1f, 0.1f, 1)).ToColor());
                }
            }

            if (b.hitRadius > 0 && (b.slowFactor < 1 || b.rustAmount > 0 || b.hitDamage > 0))
                foreach (Vessel v in near)
                {
                    Part hit = null; float best = float.MaxValue;
                    foreach (Part p in v.parts)
                    {
                        if (p == null) continue;
                        float d = (p.transform.position - at).sqrMagnitude;
                        if (d < best) { best = d; hit = p; }
                    }
                    if (hit != null && best <= b.hitRadius * b.hitRadius)
                        TachyonHit.Apply(hit, b.slowFactor, b.slowSeconds, b.slowSeconds > 0 ? 0 : b.rustAmount,
                                         b.slowSeconds > 0 ? b.rustAmount / b.slowSeconds : 0, b.hitDamage);
                }
        }

        /// <summary>
        /// A thin layer of light sweeping over a craft's own parts from <paramref name="at"/> out to its far end (the Kraken,
        /// the Blue Rinse); without the plugin's shaders, a coloured shell through VFX Reconfigured.
        /// </summary>
        void ContourWave(Vessel v, Vector3 at, Color colour)
        {
            var w = new Wave { Start = Time.time, Colour = colour };
            FfwShaders.Load();
            if (FfwShaders.Wave != null)
            {
                float far = 0;
                foreach (Part p in v.parts) if (p != null) far = Mathf.Max(far, Vector3.Distance(p.transform.position, at) + 3);
                w.Where = Visuals.ToFrame(at); w.Radius = far; w.Seconds = Mathf.Clamp(far / 12, 0.8f, 2.5f);
                w.Overlays = new List<PartOverlay>();
                foreach (Part p in v.parts) if (p != null) { var o = new PartOverlay(p, FfwShaders.Wave, "FFW contour wave"); if (!o.Empty) w.Overlays.Add(o); else o.Destroy(); }
            }
            else
            {
                float size = Mathf.Max(10, v.vesselSize.magnitude);
                w.Where = Visuals.ToFrame(v.CoM); w.Seconds = 0.9f; w.Radius = size * 1.2f;
            }
            waves.Add(w);
        }

        static float ClosestPart(Vessel v, Vector3 at)
        {
            float best = float.MaxValue;
            foreach (Part p in v.parts) if (p != null) best = Mathf.Min(best, Vector3.Distance(p.transform.position, at));
            return best;
        }

        static int KillCrew(Vessel v)
        {
            int killed = 0;
            if (v.isEVA)
            {
                if (v.rootPart != null) { v.rootPart.explode(); killed = 1; }
                return killed;
            }
            foreach (Part p in v.parts)
            {
                if (p == null) continue;
                foreach (ProtoCrewMember c in new List<ProtoCrewMember>(p.protoModuleCrew))
                {
                    p.RemoveCrewmember(c);
                    c.Die();
                    killed++;
                }
            }
            if (killed > 0)
            {
                v.CrewListSetDirty();
                GameEvents.onVesselCrewWasModified.Fire(v);
            }
            return killed;
        }

        void AddArcs(Vector3 at, float radius, float seconds, Color c)
        {
            var a = new Arcs { Where = Visuals.ToFrame(at), Start = Time.time, Seconds = seconds, Radius = radius, Lines = new LineRenderer[7] };
            a.Mat = Visuals.Additive(Visuals.Ribbon, new Color(c.r, c.g, c.b, 0.5f));
            for (int i = 0; i < a.Lines.Length; i++)
            {
                var go = new GameObject("FFW arc");
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = a.Mat; lr.positionCount = 10; lr.useWorldSpace = true;
                lr.widthMultiplier = Mathf.Max(0.15f, radius * 0.02f);
                lr.startColor = lr.endColor = Color.white;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                a.Lines[i] = lr;
            }
            arcs.Add(a);
        }

        static Light MakeLight(Vector3 where, Color c)
        {
            var go = new GameObject("FFW flash light");
            go.transform.position = where;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = 0; l.shadows = LightShadows.None;
            l.range = FallbackLightRange;
            return l;
        }

        /// <summary>The seismic ring: a flat band of light, rebuilt to its radius every frame (DrawRing).</summary>
        void MakeRing(Field f)
        {
            f.RingMesh = new Mesh { name = "FFW seismic ring" };
            f.RingMesh.MarkDynamic();
            f.RingMat = Visuals.Additive(Visuals.White, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            var go = new GameObject("FFW seismic ring");
            go.transform.SetParent(f.Go.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = f.RingMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = f.RingMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            f.RingT = go.transform;
        }

        const int RingSegments = 192;
        static readonly float[] RingOffsets = { -1, -0.2f, 0, 0.2f, 1 };        // across the band (x its half-width)
        static readonly float[] RingGlow = { 0, 0.6f, 1, 0.6f, 0 };

        static void DrawRing(Field f, Vector3 centre, float radius, float alpha)
        {
            if (f.RingMesh == null) return;
            bool on = radius > 0.5f && alpha > 0.003f;
            f.RingT.gameObject.SetActive(on);
            if (!on) return;
            Vector3 up = (centre - FlightGlobals.currentMainBody.position).normalized;
            f.RingT.position = centre;
            f.RingT.rotation = Quaternion.FromToRotation(Vector3.up, up);
            float half = Mathf.Max(4, f.RingThickness * 1.2f);
            int rows = RingOffsets.Length;
            var v = new Vector3[(RingSegments + 1) * rows];
            var c = new Color[v.Length];
            var t = new int[RingSegments * (rows - 1) * 6];
            Color col = new Color(0.35f, 0.7f, 1f);
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = 2 * Mathf.PI * i / RingSegments, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float flicker = 0.85f + 0.15f * Mathf.Sin(a * 37 + Time.time * 23);
                for (int k = 0; k < rows; k++)
                {
                    float r = Mathf.Max(0, radius + RingOffsets[k] * half);
                    v[i * rows + k] = new Vector3(ca * r, 0, sa * r);
                    float g = RingGlow[k] * alpha * flicker;
                    Color cc = k == 2 ? Color.Lerp(col, Color.white, 0.6f) : col;
                    c[i * rows + k] = new Color(cc.r * g, cc.g * g, cc.b * g, g);
                }
            }
            int n = 0;
            for (int i = 0; i < RingSegments; i++)
                for (int k = 0; k < rows - 1; k++)
                {
                    int a0 = i * rows + k, b0 = (i + 1) * rows + k;
                    t[n++] = a0; t[n++] = b0; t[n++] = a0 + 1;
                    t[n++] = a0 + 1; t[n++] = b0; t[n++] = b0 + 1;
                }
            f.RingMesh.Clear();
            f.RingMesh.vertices = v; f.RingMesh.colors = c; f.RingMesh.triangles = t;
            f.RingMesh.RecalculateBounds();
        }

        void MakeSphere(Field f)
        {
            f.Sphere = Visuals.Sphere();
            f.SphereNormals = f.Sphere.vertices;
            f.SphereColours = new Color[f.SphereNormals.Length];
            f.SphereMat = Visuals.Additive(Visuals.White, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            var s = new GameObject("FFW field sphere");
            s.transform.SetParent(f.Go.transform, false);
            s.AddComponent<MeshFilter>().sharedMesh = f.Sphere;
            var mr = s.AddComponent<MeshRenderer>();
            mr.sharedMaterial = f.SphereMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            f.SphereT = s.transform;
            s.SetActive(false);
        }

        /// <summary>
        /// Air drawn in: pale, stretched wisps born on a shell just beyond the radius that rush to the centre
        /// (a shockwave played backwards).
        /// </summary>
        ParticleSystem AirParticles(Field f)
        {
            float R = (float)f.Spec.Radius, life = f.Spec.Kind == FieldKind.Warp ? 2.2f : 1.4f;
            var ps = NewParticles(f, "FFW air inflow", false, out Material mat);
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = -R * 1.1f / life;                // inward along the shell's normal: reaches the centre as it dies
            main.startSize = new ParticleSystem.MinMaxCurve(R * 0.05f, R * 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.88f, 0.95f, 0.10f), new Color(0.95f, 0.96f, 1f, 0.18f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1500;
            var shape = ps.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = R * 1.1f; shape.radiusThickness = 0;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new[] { (0f, 0f), (0.25f, 1f), (0.8f, 0.8f), (1f, 0f) });
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 1.5f;
            var em = ps.emission; em.rateOverTime = 0;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// The tachyon field's shimmer: green and white glints twinkling all through the dome, and (near) a finer one
        /// round the camera while it's inside, so the field shows up close as well as from afar.
        /// </summary>
        ParticleSystem ShimmerParticles(Field f, bool near)
        {
            float R = (float)f.Spec.Radius;
            var ps = NewParticles(f, near ? "FFW tachyon shimmer (near)" : "FFW tachyon shimmer", true, out Material mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, near ? 0.8f : 3f);
            main.startSize = near ? new ParticleSystem.MinMaxCurve(0.08f, 0.35f) : new ParticleSystem.MinMaxCurve(R * 0.004f, R * 0.012f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 1f, 0.45f, 1f), new Color(0.85f, 1f, 0.9f, 1f));
            main.simulationSpace = near ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = near ? 600 : 2500;
            var shape = ps.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = near ? 40 : R * 0.97f; shape.radiusThickness = 1;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new[] { (0f, 0f), (0.15f, 1f), (0.35f, 0.25f), (0.55f, 1f), (0.75f, 0.3f), (0.9f, 0.9f), (1f, 0f) });   // twinkle
            var em = ps.emission; em.rateOverTime = 0;
            if (near) ps.transform.SetParent(null, true);
            ps.Play();
            return ps;
        }

        ParticleSystem NewParticles(Field f, string name, bool additive, out Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(f.Go.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.duration = 1;
            var r = go.GetComponent<ParticleSystemRenderer>();
            Shader sh = additive ? Visuals.AdditiveShader : (Shader.Find("KSP/Particles/Alpha Blended") ?? Visuals.AdditiveShader);
            mat = new Material(sh) { mainTexture = Visuals.Glow };
            mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            f.Materials.Add(mat);
            return ps;
        }

        static Gradient Fade((float t, float a)[] alphas)
        {
            var g = new Gradient();
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, ak);
            return g;
        }

        // ---- Physics ----

        /// <summary>The rigidbody velocity of a point at rest over the surface (Krakensbane and planet rotation).</summary>
        static Vector3d RestVelocity(Vector3 pos)
        {
            CelestialBody body = FlightGlobals.currentMainBody;
            Vector3d surface = body == null || body.inverseRotation ? Vector3d.zero : body.getRFrmVel(pos);
            return surface - Krakensbane.GetFrameVelocity();
        }

        void FixedUpdate()
        {
            TachyonHit.FixedTick();
            if (fields.Count == 0) return;
            float now = Time.time;
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                Field f = fields[i];
                float t = now - f.Start;
                FieldLook look = Fields.At(f.Spec, t);
                Vector3 centre = Visuals.FromFrame(f.Where);
                if (look.Pulling || look.Destroying || look.Freezing)
                {
                    doomed.Clear();
                    foreach (Vessel v in FlightGlobals.VesselsLoaded)
                    {
                        if (v == null || v.packed) continue;
                        if (look.Freezing) { Catch(f, v, centre); continue; }
                        for (int k = 0; k < v.parts.Count; k++)
                        {
                            Part p = v.parts[k];
                            if (p == null || p.rb == null) continue;
                            Vector3 d = p.rb.worldCenterOfMass - centre;
                            if (look.Destroying && Fields.Inside(f.Spec.InnerRadius, d.x, d.y, d.z)) { doomed.Add(p); continue; }
                            if (look.Pulling)
                            {
                                Fields.Pull(f.Spec, d.x, d.y, d.z, out double ax, out double ay, out double az);
                                if (ax != 0 || ay != 0 || az != 0) p.rb.AddForce(new Vector3((float)ax, (float)ay, (float)az), ForceMode.Acceleration);
                            }
                        }
                    }
                    foreach (Part p in doomed) if (p != null) p.explode();
                    if (look.Destroying && f.Buildings != null)
                        foreach (DestructibleBuilding b in f.Buildings)
                            if (b != null && !b.IsDestroyed && (b.transform.position - centre).sqrMagnitude < f.Spec.InnerRadius * f.Spec.InnerRadius) b.Demolish();
                }
                if (f.Spec.Kind == FieldKind.Tachyon)
                {
                    if (look.Slowed) foreach (TimeStop ts in f.Caught) ts.Physics(look.Speed);
                    else if (f.Caught.Count > 0) ReleaseAll(f);
                    PauseBlasts(f, centre, look);
                }

                if (f.Spec.Kind == FieldKind.Rinse && look.Killing) Rinse(f, centre, (float)look.SphereRadius);
                if (f.Spec.Kind == FieldKind.Seismic) Cut(f, centre, (float)look.RingRadius2);

                if (f.Spec.Kind == FieldKind.Gravity && !f.Blasted && t >= f.Spec.Duration)
                {
                    f.Blasted = true;
                    if (f.Spec.BlastTntKg > 0)
                    {
                        // The damage is BDArmory's; the look is the pulse drawn here, not a fireball.
                        Vfxr.SetLookAt(centre, 60, "none", 1);
                        Bda.CreateExplosion(centre, (float)f.Spec.BlastTntKg, f.BlastModel, f.BlastSound, f.Source, f.Weapon);
                    }
                }
                // (the rising sweep as the bomb's pieces fly back together: it ends as they snap into one, at the end of
                // the speed-up)
                if (f.Spec.Kind == FieldKind.Tachyon && !f.EndSound && t >= f.Spec.Duration + Fields.SpeedUp - EndSweepSeconds)
                {
                    f.EndSound = true;
                    f.Audio.PlayOneShot(EndSound());
                }
                if (look.Over) { Remove(f); fields.RemoveAt(i); }
            }
        }

        /// <summary>The Blue Rinse reaching a craft: every Kerbal aboard dies, and a blue wave passes over its hull.</summary>
        void Rinse(Field f, Vector3 centre, float radius)
        {
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || f.Rinsed.Contains(v) || ClosestPart(v, centre) > radius) continue;
                f.Rinsed.Add(v);
                int killed = KillCrew(v);
                if (killed > 0) Log.Info($"Blue Rinse: {killed} Kerbal(s) aboard {v.vesselName} killed.");
                if (killed > 0 && !v.isEVA) ContourWave(v, centre, new Color(0.3f, 0.6f, 1f));
            }
        }

        /// <summary>
        /// The seismic ring passing: every part whose centre lies within the ring's thickness of its plane, between where
        /// the ring was last step and where it is now, takes the damage once (a force field can stop it); buildings in its
        /// path come down.
        /// </summary>
        void Cut(Field f, Vector3 centre, float ring)
        {
            Vector3 up = (centre - FlightGlobals.currentMainBody.position).normalized;
            float from = f.LastRing - 2, to = ring + 1, thick = f.RingThickness;
            f.LastRing = ring;
            if (ring <= 0) return;
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null) continue;
                foreach (Part p in v.parts)
                {
                    if (p == null || f.RingHit.Contains(p)) continue;
                    Vector3 d = p.transform.position - centre;
                    float h = Vector3.Dot(d, up), r = (d - up * h).magnitude;
                    if (Mathf.Abs(h) > thick + 1 || r < from || r > to) continue;
                    f.RingHit.Add(p);
                    Bda.AddDamage(p, f.RingDamage);
                    SmallFlash(p.transform.position, f.Edge, 3, 0.15f);
                }
            }
            if (f.Buildings != null)
                foreach (DestructibleBuilding b in f.Buildings)
                {
                    if (b == null || b.IsDestroyed) continue;
                    Vector3 d = b.transform.position - centre;
                    float h = Vector3.Dot(d, up), r = (d - up * h).magnitude;
                    if (Mathf.Abs(h) < thick + 15 && r >= from && r <= to) b.Demolish();
                }
        }

        /// <summary>A craft whose centre is inside the field is caught (unless it has an active Tachyon-be-gone).</summary>
        void Catch(Field f, Vessel v, Vector3 centre)
        {
            if (f.CaughtVessels.Contains(v)) return;
            Vector3 d = v.CoM - centre;
            if (!Fields.Inside(f.Spec.Radius, d.x, d.y, d.z) || ModuleFFWTachyonShield.Protects(v)) return;
            TachyonHit.Cancel(v);   // (a tachyon-slowed craft: the field takes over)
            f.CaughtVessels.Add(v);
            var ts = new TimeStop(v, f.CaughtParts);
            if (ts.Parts == 0) return;
            f.Caught.Add(ts);
            Log.Info($"Tachyon field caught {v.vesselName} ({v.srfSpeed:0} m/s, {ts.Parts} parts{(v.isEVA ? ", kerbal on EVA" : "")}).");
        }

        static void ReleaseAll(Field f)
        {
            foreach (TimeStop ts in f.Caught)
            {
                ts.Release();
                if (ts.Vessel != null) Log.Info($"Tachyon field released {ts.Vessel.vesselName}.");
            }
            f.Caught.Clear();
            f.CaughtVessels.Clear();
            f.CaughtParts.Clear();
        }

        /// <summary>
        /// BDArmory explosions inside the field stop while time is stopped (their damage included: they work out their
        /// blast from the time since they went off, so that clock is moved on by the pause when they resume).
        /// </summary>
        void PauseBlasts(Field f, Vector3 centre, FieldLook look)
        {
            float now = Time.time;
            if (look.Slowed && look.Speed < 0.05)
            {
                Bda.LiveExplosions(live);
                float r2 = (float)(f.Spec.Radius * f.Spec.Radius);
                foreach (var (fx, start) in live)
                    if (fx.enabled && (fx.transform.position - centre).sqrMagnitude <= r2)
                    {
                        fx.enabled = false;
                        f.PausedBlasts.Add((fx, start, now));
                    }
            }
            else ResumeBlasts(f);
        }

        static void ResumeBlasts(Field f)
        {
            float now = Time.time;
            foreach (var (fx, start, at) in f.PausedBlasts)
            {
                if (fx == null) continue;
                try { start.SetValue(fx, (float)start.GetValue(fx, null) + (now - at), null); } catch { }
                fx.enabled = true;
            }
            f.PausedBlasts.Clear();
        }

        // ---- Looks ----        // ---- Looks ----

        void LateUpdate()
        {
            float now = Time.time;
            Camera cam = Visuals.Camera;
            foreach (Field f in fields)
            {
                float t = now - f.Start;
                FieldLook look = Fields.At(f.Spec, t);
                Vector3 centre = Visuals.FromFrame(f.Where);
                f.Go.transform.position = centre;
                if (look.LensStrength > 1e-3 || look.RingAmp > 1e-3)
                    Vfxr.LensCore(centre, (float)look.LensRadius, (float)look.LensStrength, (float)look.CoreShare, (float)look.RingRadius, (float)look.RingAmp);
                if (look.InRingAmp > 1e-3)
                    Vfxr.LensCore(centre, (float)f.Spec.Radius, 0, 0, (float)look.InRingRadius, (float)look.InRingAmp);
                if (look.Flash > 0.003)
                    Vfxr.Glow(centre, (float)look.FlashRadius, (float)look.Flash, 0.2f, f.Edge, f.Core);
                if (f.Light != null) { f.Light.transform.position = centre; f.Light.intensity = Mathf.Min(8, (float)look.Flash * 3); }
                if (f.Sphere != null) DrawSphere(f, look, centre, cam, t);
                if (f.RingMesh != null) DrawRing(f, centre, (float)look.RingRadius2, (float)look.RingAlpha2);
                if (f.Spec.Kind == FieldKind.Tachyon)
                {
                    if (look.Slowed) Vfxr.TimeScale(centre, (float)f.Spec.Radius, (float)look.Speed);
                    foreach (TimeStop ts in f.Caught) ts.Effects(look.Speed, Time.deltaTime);
                    if (f.Shatter != null)
                    {
                        double x = System.Math.Min(1, look.Tau / 0.35);
                        f.Shatter.Tick((float)(1 - (1 - x) * (1 - x) * (1 - x)), !look.Slowed, Time.deltaTime);
                    }
                }
                if (f.Air != null) { var em = f.Air.emission; em.rateOverTime = look.Pulling ? (f.Spec.Kind == FieldKind.Warp ? 260 : 160) : 0; }
                if (f.Shimmer != null) { var em = f.Shimmer.emission; em.rateOverTime = look.Freezing ? 900 : 0; }
                if (f.NearShimmer != null)
                {
                    bool inside = cam != null && (cam.transform.position - centre).sqrMagnitude < f.Spec.Radius * f.Spec.Radius;
                    if (cam != null) f.NearShimmer.transform.position = cam.transform.position;
                    var em = f.NearShimmer.emission; em.rateOverTime = look.Freezing && inside ? 300 : 0;
                }
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                Flash fl = flashes[i];
                float age = now - fl.Start;
                if (age > fl.Seconds)
                {
                    if (fl.Light != null) Destroy(fl.Light.gameObject);
                    flashes.RemoveAt(i);
                    continue;
                }
                Vector3 at = Visuals.FromFrame(fl.Where);
                float p = 2.5f * Mathf.Exp(-age / (fl.Seconds * 0.3f)) * (1 - age / fl.Seconds);
                Vfxr.Glow(at, fl.Radius, p, 0.2f, fl.Edge, fl.Core);
                if (fl.Light != null) { fl.Light.transform.position = at; fl.Light.intensity = Mathf.Min(8, p * 3); }
            }
            Targets.Tick();
            TachyonHit.Tick();
            for (int i = lenses.Count - 1; i >= 0; i--)
            {
                PhaseLensFx l = lenses[i];
                float x = (now - l.Start) / PhaseLensSeconds;
                if (x >= 1) { Destroy(l.Go); Destroy(l.Mat); lenses.RemoveAt(i); continue; }
                // Snaps open, then closes; the colours stay torn apart longer than the bending lasts.
                float k = Mathf.Clamp01(x / 0.08f) * (1 - x) * (1 - x);
                float chroma = Mathf.Clamp01(x / 0.05f) * (1 - x);
                l.Go.transform.position = Visuals.FromFrame(l.Where);
                l.Mat.SetVector(PhaseLensId, new Vector4(PhaseLensRadius * (0.6f + 0.6f * x), 1.6f * k, 1.4f * chroma, 0));
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple r = ripples[i];
                float x = (now - r.Start) / r.Seconds;
                if (x >= 1) { ripples.RemoveAt(i); continue; }
                // (a negative radius: a ring collapsing inward, an implosion; positive: spreading outward)
                float R = Mathf.Abs(r.Radius), ring = r.Radius < 0 ? R * (1.4f - 1.3f * x) : R * (0.2f + 1.3f * x);
                Vfxr.LensCore(Visuals.FromFrame(r.Where), R, r.Radius < 0 ? 0.3f * (1 - x) : 0, 0, ring, 1.2f * (1 - x));
            }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                Wave w = waves[i];
                float x = (now - w.Start) / w.Seconds;
                if (x >= 1.25f || w.Overlays == null && x >= 1)
                {
                    if (w.Overlays != null) foreach (PartOverlay o in w.Overlays) o.Destroy();
                    waves.RemoveAt(i); continue;
                }
                if (w.Overlays != null)
                {
                    // (the band runs past the far end, then its trail fades)
                    float fade = x <= 1 ? 1 : 1 - (x - 1) / 0.25f;
                    Vector3 o0 = Visuals.FromFrame(w.Where);
                    foreach (PartOverlay o in w.Overlays)
                    {
                        o.Block.SetVector(WaveId, new Vector4(o0.x, o0.y, o0.z, w.Radius * Mathf.Min(x, 1.1f)));
                        o.Block.SetVector(WaveShapeId, new Vector4(0.35f, 3f, 2.2f * fade, 1));
                        o.Block.SetColor(WaveColourId, w.Colour);
                        o.Apply();
                    }
                    continue;
                }
                // (fallback without the plugin's shaders) a shell of red light sweeping out over the craft.
                Vfxr.TintSphere(Visuals.FromFrame(w.Where), Mathf.Max(0.5f, w.Radius * x), new Color(1, 0.35f, 0.3f), 0.35f * (1 - x), w.Colour, 3 * (1 - x * x));
            }
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                Arcs a = arcs[i];
                float x = (now - a.Start) / a.Seconds;
                if (x >= 1)
                {
                    foreach (LineRenderer lr in a.Lines) if (lr != null) Destroy(lr.gameObject);
                    Destroy(a.Mat); arcs.RemoveAt(i); continue;
                }
                Vector3 c = Visuals.FromFrame(a.Where);
                foreach (LineRenderer lr in a.Lines)
                {
                    // Each arc a fresh jagged bolt every frame, flickering on and off.
                    lr.enabled = Random.value < 0.75f;
                    Vector3 end = c + Random.onUnitSphere * a.Radius * Random.Range(0.4f, 1f);
                    for (int k = 0; k < lr.positionCount; k++)
                    {
                        float f = (float)k / (lr.positionCount - 1);
                        Vector3 p = Vector3.Lerp(c, end, f);
                        if (k > 0 && k < lr.positionCount - 1) p += Random.insideUnitSphere * a.Radius * 0.08f;
                        lr.SetPosition(k, p);
                    }
                    lr.widthMultiplier = Mathf.Max(0.15f, a.Radius * 0.02f) * (1 - x);
                }
            }
            for (int i = duds.Count - 1; i >= 0; i--)
            {
                duds[i].Tick(0, true, Time.deltaTime);
                if (duds[i].Done) duds.RemoveAt(i);
            }
        }

        static readonly Color TachyonTint = new Color(0.55f, 1f, 0.62f), TachyonEdge = new Color(0.3f, 1f, 0.45f);

        static void DrawSphere(Field f, FieldLook look, Vector3 centre, Camera cam, float t)
        {
            float r = (float)look.SphereRadius;
            bool on = r > 0.5f && look.SphereAlpha > 0.003;
            // The tachyon field through VFX Reconfigured: everything inside tinted green, a crisp edge seen from inside
            // and out. (Its own sphere below is the fallback.)
            if (on && f.Spec.Kind == FieldKind.Tachyon &&
                Vfxr.TintSphere(centre, r, TachyonTint, 0.6f * (float)look.SphereAlpha, TachyonEdge, 1.2f * (float)look.SphereAlpha)) on = false;
            // The Blue Rinse: a faint blue inside, a bright electric-blue edge.
            if (on && f.Spec.Kind == FieldKind.Rinse &&
                Vfxr.TintSphere(centre, r, new Color(0.7f, 0.82f, 1f), 0.3f * (float)look.SphereAlpha, new Color(0.3f, 0.6f, 1f), 2f * (float)look.SphereAlpha)) on = false;
            f.SphereT.gameObject.SetActive(on);
            if (!on) return;
            f.SphereT.localScale = Vector3.one * r;
            Vector3 eye = cam != null ? (cam.transform.position - centre) / r : Vector3.up * 10;
            float a = (float)look.SphereAlpha;
            Color c = f.SphereColour;
            for (int i = 0; i < f.SphereNormals.Length; i++)
            {
                Vector3 n = f.SphereNormals[i];
                Vector3 view = (n - eye).normalized;
                float rim = 1 - Mathf.Abs(Vector3.Dot(n, view));
                // (a firm shell, readable from inside too, with a crisp rim)
                float k = a * (0.2f + 0.8f * Mathf.Pow(rim, 6) + 0.3f * Mathf.Pow(rim, 40)) * (0.85f + 0.15f * Mathf.Sin(n.y * 25 + t * 3));
                f.SphereColours[i] = new Color(c.r * k, c.g * k, c.b * k, k);
            }
            f.Sphere.colors = f.SphereColours;
        }

        void Remove(Field f)
        {
            f.Removed = true;
            if (f.Caught.Count > 0) ReleaseAll(f);
            ResumeBlasts(f);
            if (f.Shatter != null) { if (!SceneChanging && !f.Shatter.Done) { f.Shatter.Tick(0, true, 0); duds.Add(f.Shatter); } else f.Shatter.Clear(); f.Shatter = null; }
            if (f.Light != null) Destroy(f.Light.gameObject);
            if (f.SphereT != null) f.SphereT.gameObject.SetActive(false);
            foreach (ParticleSystem ps in new[] { f.Air, f.Shimmer, f.NearShimmer })
                if (ps != null) { var em = ps.emission; em.rateOverTime = 0; }
            if (f.NearShimmer != null) Destroy(f.NearShimmer.gameObject, 3);
            if (f.Go != null) Destroy(f.Go, 3);   // (let the last sound and particles finish)
            if (f.Sphere != null) Destroy(f.Sphere);
            if (f.RingMesh != null) Destroy(f.RingMesh);
            if (f.RingMat != null) Destroy(f.RingMat);
            if (f.SphereMat != null) Destroy(f.SphereMat);
            foreach (Material m in f.Materials) Destroy(m, 3.5f);
        }

        void OnDestroy()
        {
            GameEvents.onGameSceneSwitchRequested.Remove(OnSceneSwitch);
            foreach (Field f in fields) Remove(f);
            fields.Clear();
            foreach (Flash fl in flashes) if (fl.Light != null) Destroy(fl.Light.gameObject);
            flashes.Clear();
            foreach (ShatterFx d in duds) d.Clear();
            duds.Clear();
            foreach (Arcs a in arcs) { foreach (LineRenderer lr in a.Lines) if (lr != null) Destroy(lr.gameObject); Destroy(a.Mat); }
            foreach (Wave w in waves) if (w.Overlays != null) foreach (PartOverlay o in w.Overlays) o.Destroy();
            foreach (PhaseLensFx l in lenses) { if (l.Go != null) Destroy(l.Go); if (l.Mat != null) Destroy(l.Mat); }
            lenses.Clear();
            if (lensQuad != null) Destroy(lensQuad);
            arcs.Clear(); ripples.Clear(); waves.Clear();
            Targets.Clear();
            TachyonHit.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
