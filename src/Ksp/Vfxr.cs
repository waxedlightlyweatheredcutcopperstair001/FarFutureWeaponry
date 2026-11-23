using System;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// VFX Reconfigured's EffectsApi (0.8.0 and later), bound by reflection: gravitational lenses and glows drawn over
    /// everything, and looks for weapon parts' explosions. Missing (older VFX Reconfigured): the calls do nothing and
    /// the plugin falls back on its own lights.
    /// </summary>
    internal static class Vfxr
    {
        static bool initialised;
        static Func<Vector3, float, float, float, float, bool> lens;
        static Func<Vector3, float, float, float, Color, Color, bool> glow;
        static Action<Part, string> setLook;
        static Func<Vector3, float, float, float, float, float, bool> lensCore;   // EffectsApi 2
        static Action<Vector3, float, string, float> setLookAt;                    // EffectsApi 2
        static Func<Vector3, float, Color, float, Color, float, bool> tintSphere;  // EffectsApi 3
        static Action<Vector3, float, float> timeScale;                            // EffectsApi 3

        public static bool Present { get; private set; }

        public static void Init()
        {
            if (initialised) return;
            initialised = true;
            Assembly vfxr = null;
            foreach (var la in AssemblyLoader.loadedAssemblies)
                if (la.name == "VolumetricFXReconfigured") { vfxr = la.assembly; break; }
            Type api = vfxr?.GetType("VFXReconfigured.EffectsApi");
            if (api == null)
            {
                Log.Warn(vfxr == null ? "VFX Reconfigured is not installed: no lenses, flashes or weapon looks."
                                      : "VFX Reconfigured is older than 0.8.0 (no EffectsApi): no lenses, flashes or weapon looks. Update it.");
                return;
            }
            try
            {
                lens = (Func<Vector3, float, float, float, float, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, float, float, float, float, bool>),
                    api.GetMethod("Lens", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) }));
                glow = (Func<Vector3, float, float, float, Color, Color, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, float, float, float, Color, Color, bool>),
                    api.GetMethod("Glow", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(Color), typeof(Color) }));
                setLook = (Action<Part, string>)Delegate.CreateDelegate(typeof(Action<Part, string>),
                    api.GetMethod("SetPartLook", new[] { typeof(Part), typeof(string) }));
                MethodInfo m = api.GetMethod("LensCore", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) });
                if (m != null) lensCore = (Func<Vector3, float, float, float, float, float, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, float, float, float, float, float, bool>), m);
                m = api.GetMethod("SetLookAt", new[] { typeof(Vector3), typeof(float), typeof(string), typeof(float) });
                if (m != null) setLookAt = (Action<Vector3, float, string, float>)Delegate.CreateDelegate(typeof(Action<Vector3, float, string, float>), m);
                m = api.GetMethod("TintSphere", new[] { typeof(Vector3), typeof(float), typeof(Color), typeof(float), typeof(Color), typeof(float) });
                if (m != null) tintSphere = (Func<Vector3, float, Color, float, Color, float, bool>)Delegate.CreateDelegate(typeof(Func<Vector3, float, Color, float, Color, float, bool>), m);
                m = api.GetMethod("TimeScale", new[] { typeof(Vector3), typeof(float), typeof(float) });
                if (m != null) timeScale = (Action<Vector3, float, float>)Delegate.CreateDelegate(typeof(Action<Vector3, float, float>), m);
                if (tintSphere == null || timeScale == null) Log.Warn("VFX Reconfigured's EffectsApi is older than version 3: the tachyon field draws its own plain sphere and doesn't slow explosions' visuals. Update VFX Reconfigured to 0.10.0.");
                if (lensCore == null || setLookAt == null) Log.Warn("VFX Reconfigured's EffectsApi is version 1: the gravity bomb keeps a full black core and its closing blast is drawn as a fireball. Update VFX Reconfigured to 0.9.0.");
                Present = true;
                object version = api.GetField("Version")?.GetValue(null);
                Log.Info($"VFX Reconfigured EffectsApi {version} found.");
            }
            catch (Exception e)
            {
                Log.Warn("VFX Reconfigured's EffectsApi couldn't be bound: " + e.Message);
                lens = null; glow = null; setLook = null;
            }
        }

        public static bool Lens(Vector3 position, float radius, float strength, float ringRadius, float ringAmp) =>
            lens != null && lens(position, radius, strength, ringRadius, ringAmp);

        /// <summary>A lens with its black core's size (EffectsApi 2); on version 1, the usual core.</summary>
        public static bool LensCore(Vector3 position, float radius, float strength, float coreShare, float ringRadius, float ringAmp) =>
            lensCore != null ? lensCore(position, radius, strength, coreShare, ringRadius, ringAmp) : Lens(position, radius, strength, ringRadius, ringAmp);

        /// <summary>A look for explosions near a point for a while (EffectsApi 2); nothing on version 1.</summary>
        public static void SetLookAt(Vector3 position, float within, string look, float seconds) => setLookAt?.Invoke(position, within, look, seconds);

        /// <summary>A sphere tinting what's inside it, with a visible edge (EffectsApi 3); false when it can't be drawn.</summary>
        public static bool TintSphere(Vector3 centre, float radius, Color tint, float strength, Color edge, float edgeStrength) =>
            tintSphere != null && tintSphere(centre, radius, tint, strength, edge, edgeStrength);

        /// <summary>Explosions' visuals in a zone run at this speed this frame (EffectsApi 3).</summary>
        public static void TimeScale(Vector3 centre, float radius, float scale) => timeScale?.Invoke(centre, radius, scale);

        public static bool Glow(Vector3 position, float radius, float power, float coreShare, Color edge, Color core) =>
            glow != null && glow(position, radius, power, coreShare, edge, core);

        public static void SetPartLook(Part part, string look)
        {
            if (setLook != null && part != null) setLook(part, look);
        }
    }
}
