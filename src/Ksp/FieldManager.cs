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

        static AudioClip StartSound(FieldSpec s) => s.Kind == FieldKind.Tachyon
            ? Clip("tachyon-start", () => FieldSound.Sweep(Rate, 0.9f, 1400, 140, 3))
            : Clip(Key("rumble", s.Duration), () => FieldSound.Rumble(Rate, (float)s.Duration + 0.3f, true, 5));

        static AudioClip EndSound() => Clip("tachyon-end", () => FieldSound.Sweep(Rate, 0.7f, 140, 1500, 4));

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

        internal void Detonation(ModuleFFWBomb bomb, FieldSpec spec, Vector3 where, string source)
        {
            float now = Time.time;
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

            if (spec.Kind == FieldKind.Tachyon || spec.Kind == FieldKind.Gravity) MakeSphere(f);
            if (spec.Kind == FieldKind.Gravity || spec.Kind == FieldKind.Warp) f.Air = AirParticles(f);
            if (spec.Kind == FieldKind.Tachyon)
            {
                f.Shimmer = ShimmerParticles(f, false);
                f.NearShimmer = ShimmerParticles(f, true);
                if (bomb.shatter) f.Shatter = ShatterFx.Capture(bomb.part);
            }
            if (spec.Kind == FieldKind.Warp && spec.InnerRadius > 0)
            {
                f.Buildings = new List<DestructibleBuilding>();
                foreach (DestructibleBuilding b in FindObjectsOfType<DestructibleBuilding>())
                    if (b != null && (b.transform.position - where).sqrMagnitude < (spec.Radius * spec.Radius * 4)) f.Buildings.Add(b);
            }
            fields.Add(f);
            Log.Info($"{spec.Kind} field from {f.Weapon}: radius {spec.Radius:0} m, {spec.Duration:0.#} s" +
                     (Vfxr.Present ? "" : " (no VFX Reconfigured EffectsApi: no lens or flash)"));
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
                if (f.Spec.Kind == FieldKind.Tachyon && !f.EndSound && t >= f.Spec.Duration)
                {
                    f.EndSound = true;
                    f.Audio.PlayOneShot(EndSound());
                }
                if (look.Over) { Remove(f); fields.RemoveAt(i); }
            }
        }

        /// <summary>A craft whose centre is inside the field is caught (unless it has an active Tachyon-be-gone).</summary>
        void Catch(Field f, Vessel v, Vector3 centre)
        {
            if (f.CaughtVessels.Contains(v)) return;
            Vector3 d = v.CoM - centre;
            if (!Fields.Inside(f.Spec.Radius, d.x, d.y, d.z) || ModuleFFWTachyonShield.Protects(v)) return;
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
            if (Instance == this) Instance = null;
        }
    }
}
