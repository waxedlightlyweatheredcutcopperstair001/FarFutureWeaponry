using System.Collections;
using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Bright coloured flashes where a weapon's shots explode.
    ///
    /// BDArmory has no hit events, but every explosion it makes comes from a pool keyed by the weapon's explModelPath.
    /// Each FFW weapon has its own copy of the explosion model, so its explosions land in their own pool; this watches
    /// those pools (FFW_IMPACT_FLASH nodes name them) and adds a flash and a light each time one goes off.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ImpactFlashes : MonoBehaviour
    {
        sealed class FlashDef
        {
            public string Explosion;
            public Color Color;
            public float Size = 6, Duration = 0.5f, LightRange = 40, LightIntensity = 4;
            public float SlowFactor = 1, SlowSeconds, Rust, Damage;   // tachyon hits (TachyonHit): slowing, rust over the craft, damage to the part hit
            public int Sparkles;                      // glints thrown out where it hits
            public readonly Dictionary<GameObject, float> Seen = new Dictionary<GameObject, float>();
        }

        sealed class Flash
        {
            public GameObject Go;
            public Transform T;
            public Material Mat;
            public Light Light;
            public FlashDef Def;
            public Vector3d Where;
            public float Start;
            public bool Busy;
        }

        readonly List<FlashDef> defs = new List<FlashDef>();
        readonly List<Flash> flashes = new List<Flash>();
        Mesh quad;

        void Start()
        {
            Bda.Init();
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("FFW_IMPACT_FLASH"))
            {
                var d = new FlashDef { Explosion = n.GetValue("explosion") };
                if (string.IsNullOrEmpty(d.Explosion)) continue;
                d.Color = Rgba.Parse(n.GetValue("color"), new Rgba(1, 1, 1, 1)).ToColor();
                n.TryGetValue("size", ref d.Size);
                n.TryGetValue("duration", ref d.Duration);
                n.TryGetValue("lightRange", ref d.LightRange);
                n.TryGetValue("lightIntensity", ref d.LightIntensity);
                n.TryGetValue("slowFactor", ref d.SlowFactor);
                n.TryGetValue("slowSeconds", ref d.SlowSeconds);
                n.TryGetValue("rust", ref d.Rust);
                n.TryGetValue("damage", ref d.Damage);
                n.TryGetValue("sparkles", ref d.Sparkles);
                defs.Add(d);
            }
            if (defs.Count > 0 && !Bda.ExplosionsReadable)
            {
                Log.Warn("Impact flashes are off: BDArmory's explosion pools couldn't be read.");
                defs.Clear();
            }
            Log.Info($"Impact flashes: {defs.Count} weapon explosion type(s) watched.");
        }

        void LateUpdate()
        {
            if (defs.Count == 0) return;
            float now = Time.time;
            for (int i = 0; i < defs.Count; i++)
            {
                FlashDef d = defs[i];
                IList pool = Bda.ExplosionPool(d.Explosion);
                if (pool == null) continue;
                for (int k = 0; k < pool.Count; k++)
                {
                    var go = pool[k] as GameObject;
                    if (!Bda.ReadExplosion(go, out float start, out Vector3 pos)) continue;
                    if (d.Seen.TryGetValue(go, out float last) && last == start) continue;
                    d.Seen[go] = start;
                    if (now - start >= d.Duration * 0.5f) continue;
                    if (d.Size > 0) Spawn(d, pos, now);
                    if (d.Sparkles > 0) Visuals.Sparkle(pos, d.Sparkles, d.Color, 0.18f, 4);
                    if (d.SlowFactor < 1 || d.Rust > 0 || d.Damage > 0) TachyonHit.Apply(Bda.ExplosionHitPart(go), d.SlowFactor, d.SlowSeconds, d.Rust, 0, d.Damage);
                }
            }
            Camera cam = Visuals.Camera;
            for (int i = 0; i < flashes.Count; i++)
            {
                Flash f = flashes[i];
                if (!f.Busy) continue;
                float t = now - f.Start, dur = f.Def.Duration;
                float k = FxCurves.Flash(t, dur);
                if (t >= dur)
                {
                    f.Busy = false;
                    f.Go.SetActive(false);
                    continue;
                }
                f.T.position = Visuals.FromFrame(f.Where);
                if (cam != null) f.T.rotation = cam.transform.rotation;
                f.T.localScale = Vector3.one * (f.Def.Size * FxCurves.FlashSize(t, dur));
                Color c = f.Def.Color;
                f.Mat.SetColor("_TintColor", new Color(c.r, c.g, c.b, c.a * k * 0.5f));   // additive particles double the tint
                f.Light.intensity = f.Def.LightIntensity * k;
            }
        }

        void Spawn(FlashDef d, Vector3 pos, float now)
        {
            Flash f = null;
            for (int i = 0; i < flashes.Count; i++)
                if (!flashes[i].Busy) { f = flashes[i]; break; }
            if (f == null)
            {
                if (flashes.Count >= 64) return;   // a storm of hits: the oldest flashes are still up, skip this one
                if (quad == null) quad = Visuals.Quad();
                var go = new GameObject("FFW impact flash");
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = Visuals.Additive(Visuals.Glow, Color.white);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                f = new Flash { Go = go, T = go.transform, Mat = mat, Light = light };
                flashes.Add(f);
            }
            f.Def = d;
            f.Where = Visuals.ToFrame(pos);
            f.Start = now;
            f.Busy = true;
            f.Light.color = d.Color;
            f.Light.range = d.LightRange;
            f.Light.intensity = 0;
            f.T.position = pos;
            f.Go.SetActive(true);
        }

        void OnDestroy()
        {
            foreach (Flash f in flashes)
            {
                if (f.Go != null) Destroy(f.Go);
                if (f.Mat != null) Destroy(f.Mat);
            }
            flashes.Clear();
            if (quad != null) Destroy(quad);
        }
    }
}
