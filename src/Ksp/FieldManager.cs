using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Runs the bombs' fields after they go off (ModuleFFWBomb): the pull, the black hole's destruction, the tachyon
    /// freeze, the blast at the end of a gravity well, and how they look and sound (timelines in Core/Fields). Lenses and
    /// flashes are drawn by VFX Reconfigured's EffectsApi; the tachyon sphere, lights and sounds by the plugin.
    /// Positions are kept in the main body's frame (floating origin).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }
        public static bool SceneChanging { get; private set; }

        sealed class Frozen
        {
            public readonly Dictionary<Part, (Vector3d vel, Vector3 ang)> Parts = new Dictionary<Part, (Vector3d, Vector3)>();
        }

        sealed class Field
        {
            public FieldSpec Spec;
            public Vector3d Where;
            public float Start;
            public string Source, Weapon, BlastModel, BlastSound;
            public bool Blasted, EndSound;
            public Color Edge, Core;
            public GameObject Go;
            public Light Light;
            public AudioSource Audio;
            public readonly List<AudioClip> Clips = new List<AudioClip>();
            public Mesh Sphere;
            public Material SphereMat;
            public Color[] SphereColours;
            public Vector3[] SphereNormals;
            public List<DestructibleBuilding> Buildings;
            public readonly Dictionary<Vessel, Frozen> Frozen = new Dictionary<Vessel, Frozen>();
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
        readonly List<ShatterFx> shatters = new List<ShatterFx>();
        readonly List<Part> doomed = new List<Part>();
        readonly List<Vessel> gone = new List<Vessel>();

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
                    Edge = c, Core = Color.Lerp(c, Color.white, 0.7f), Light = MakeLight(where, c)
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
                case FieldKind.Gravity: f.Edge = new Color(0.75f, 0.8f, 1f); f.Core = Color.white; break;
                case FieldKind.Warp: f.Edge = new Color(0.3f, 0.5f, 1f); f.Core = new Color(0.9f, 0.95f, 1f); break;
                default: f.Edge = new Color(0.25f, 1f, 0.4f); f.Core = new Color(0.85f, 1f, 0.85f); break;
            }
            f.Go = new GameObject("FFW field " + spec.Kind);
            f.Go.transform.position = where;
            f.Light = f.Go.AddComponent<Light>();
            f.Light.type = LightType.Point; f.Light.color = f.Edge; f.Light.intensity = 0; f.Light.shadows = LightShadows.None;
            f.Audio = f.Go.AddComponent<AudioSource>();
            f.Audio.spatialBlend = 1; f.Audio.dopplerLevel = 0; f.Audio.rolloffMode = AudioRolloffMode.Logarithmic;
            f.Audio.minDistance = 30; f.Audio.maxDistance = (float)spec.Radius * 6;
            f.Audio.volume = GameSettings.SHIP_VOLUME;

            const int rate = 44100;
            if (spec.Kind == FieldKind.Tachyon)
            {
                Play(f, FieldSound.Sweep(rate, 0.9f, 1400, 140, 3));
                f.Sphere = Visuals.Sphere();
                f.SphereNormals = f.Sphere.vertices;
                f.SphereColours = new Color[f.SphereNormals.Length];
                f.SphereMat = Visuals.Additive(Visuals.White, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                var s = new GameObject("FFW tachyon sphere");
                s.transform.SetParent(f.Go.transform, false);
                s.AddComponent<MeshFilter>().sharedMesh = f.Sphere;
                var mr = s.AddComponent<MeshRenderer>();
                mr.sharedMaterial = f.SphereMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                if (bomb.shatter)
                {
                    ShatterFx fx = ShatterFx.Capture(bomb.part, (float)spec.Duration);
                    if (fx != null) shatters.Add(fx);
                }
            }
            else
            {
                Play(f, FieldSound.Rumble(rate, (float)spec.Duration + 0.3f, true, 5));
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
            return l;
        }

        static void Play(Field f, float[] samples)
        {
            var clip = AudioClip.Create("FFW field", samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            f.Clips.Add(clip);
            f.Audio.PlayOneShot(clip);
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
                        if (look.Freezing) { Freeze(f, v, centre); continue; }
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
                if (look.Freezing) Hold(f);
                else if (f.Frozen.Count > 0) ReleaseAll(f);

                if (f.Spec.Kind == FieldKind.Gravity && !f.Blasted && t >= f.Spec.Duration)
                {
                    f.Blasted = true;
                    if (f.Spec.BlastTntKg > 0) Bda.CreateExplosion(centre, (float)f.Spec.BlastTntKg, f.BlastModel, f.BlastSound, f.Source, f.Weapon);
                }
                if (f.Spec.Kind == FieldKind.Tachyon && !f.EndSound && t >= f.Spec.Duration)
                {
                    f.EndSound = true;
                    Play(f, FieldSound.Sweep(44100, 0.7f, 140, 1500, 4));
                }
                if (look.Over) { Remove(f); fields.RemoveAt(i); }
            }
        }

        void Freeze(Field f, Vessel v, Vector3 centre)
        {
            if (f.Frozen.ContainsKey(v)) return;
            Vector3 d = v.CoM - centre;
            if (!Fields.Inside(f.Spec.Radius, d.x, d.y, d.z) || ModuleFFWTachyonShield.Protects(v)) return;
            var fr = new Frozen();
            foreach (Part p in v.parts)
            {
                if (p == null || p.rb == null) continue;
                fr.Parts[p] = ((Vector3d)p.rb.velocity - RestVelocity(p.rb.position), p.rb.angularVelocity);
            }
            f.Frozen[v] = fr;
            Log.Info($"Tachyon field froze {v.vesselName} ({v.srfSpeed:0} m/s).");
        }

        static void Hold(Field f)
        {
            foreach (var kv in f.Frozen)
            {
                if (kv.Key == null || kv.Key.packed) continue;
                foreach (var pv in kv.Value.Parts)
                {
                    Part p = pv.Key;
                    if (p == null || p.rb == null) continue;
                    p.rb.velocity = RestVelocity(p.rb.position);
                    p.rb.angularVelocity = Vector3.zero;
                }
            }
        }

        static void ReleaseAll(Field f)
        {
            foreach (var kv in f.Frozen)
            {
                if (kv.Key == null) continue;
                foreach (var pv in kv.Value.Parts)
                {
                    Part p = pv.Key;
                    if (p == null || p.rb == null) continue;
                    p.rb.velocity = RestVelocity(p.rb.position) + pv.Value.vel;
                    p.rb.angularVelocity = pv.Value.ang;
                }
                Log.Info($"Tachyon field released {kv.Key.vesselName} ({kv.Key.srfSpeed:0} m/s).");
            }
            f.Frozen.Clear();
        }

        // ---- Looks ----

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
                    Vfxr.Lens(centre, (float)look.LensRadius, (float)look.LensStrength, (float)look.RingRadius, (float)look.RingAmp);
                if (look.Flash > 0.003)
                    Vfxr.Glow(centre, (float)look.FlashRadius, (float)look.Flash, 0.2f, f.Edge, f.Core);
                f.Light.intensity = (float)look.Flash * 3;
                f.Light.range = (float)Mathf.Max(50, (float)look.FlashRadius * 3);
                if (f.Sphere != null) DrawSphere(f, look, centre, cam, t);
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
                if (fl.Light != null) { fl.Light.transform.position = at; fl.Light.intensity = p * 3; fl.Light.range = fl.Radius * 2; }
            }
            for (int i = shatters.Count - 1; i >= 0; i--)
            {
                ShatterFx s = shatters[i];
                s.Tick(now - Start0(s), Time.deltaTime);
                if (s.Done) shatters.RemoveAt(i);
            }
        }

        // Shatter effects count time from their own capture.
        readonly Dictionary<ShatterFx, float> shatterStart = new Dictionary<ShatterFx, float>();
        float Start0(ShatterFx s)
        {
            if (!shatterStart.TryGetValue(s, out float t0)) shatterStart[s] = t0 = Time.time;
            return t0;
        }

        static void DrawSphere(Field f, FieldLook look, Vector3 centre, Camera cam, float t)
        {
            Transform st = f.Go.transform.GetChild(0);
            float r = (float)look.SphereRadius;
            st.gameObject.SetActive(r > 0.5f);
            if (r <= 0.5f) return;
            st.localScale = Vector3.one * r;
            Vector3 eye = cam != null ? (cam.transform.position - centre) / r : Vector3.up * 10;
            float a = (float)look.SphereAlpha;
            for (int i = 0; i < f.SphereNormals.Length; i++)
            {
                Vector3 n = f.SphereNormals[i];
                Vector3 view = (n - eye).normalized;
                float rim = 1 - Mathf.Abs(Vector3.Dot(n, view));
                float k = a * (0.05f + 0.95f * rim * rim * rim) * (0.85f + 0.15f * Mathf.Sin(n.y * 25 + t * 3));
                f.SphereColours[i] = new Color(0.25f * k, 1f * k, 0.4f * k, k);
            }
            f.Sphere.colors = f.SphereColours;
        }

        void Remove(Field f)
        {
            if (f.Frozen.Count > 0) ReleaseAll(f);
            if (f.Light != null) f.Light.enabled = false;
            if (f.Go != null && f.Go.transform.childCount > 0) f.Go.transform.GetChild(0).gameObject.SetActive(false);
            if (f.Go != null) Destroy(f.Go, 3);   // (let the last sound finish)
            if (f.Sphere != null) Destroy(f.Sphere);
            if (f.SphereMat != null) Destroy(f.SphereMat);
            foreach (AudioClip c in f.Clips) Destroy(c, 3);
        }

        void OnDestroy()
        {
            GameEvents.onGameSceneSwitchRequested.Remove(OnSceneSwitch);
            foreach (Field f in fields) Remove(f);
            fields.Clear();
            foreach (Flash fl in flashes) if (fl.Light != null) Destroy(fl.Light.gameObject);
            flashes.Clear();
            foreach (ShatterFx s in shatters) s.Clear();
            shatters.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
