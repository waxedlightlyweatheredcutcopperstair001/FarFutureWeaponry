using System;
using System.Linq;
using System.Reflection;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Harmony (the HarmonyKSP mod, assembly 0Harmony), bound by reflection so the plugin builds without it. Used to put
    /// prefixes on a few BDArmory methods: the force field (damage), the cloak (radar pings). Missing: those features
    /// say so in the log and do without.
    /// </summary>
    internal static class HarmonyBridge
    {
        static bool initialised;
        static object harmony;
        static Type methodType;
        static MethodInfo patch;

        public static bool Present { get; private set; }

        public static void Init()
        {
            if (initialised) return;
            initialised = true;
            Assembly h = null;
            foreach (var la in AssemblyLoader.loadedAssemblies)
                if (la.name == "0Harmony") { h = la.assembly; break; }
            Type harmonyType = h?.GetType("HarmonyLib.Harmony");
            methodType = h?.GetType("HarmonyLib.HarmonyMethod");
            if (harmonyType == null || methodType == null)
            {
                Log.Warn("Harmony (HarmonyKSP) is not installed: the force field can't block damage and cloaks don't hide radar emissions.");
                return;
            }
            try
            {
                harmony = Activator.CreateInstance(harmonyType, "FarFutureWeaponry");
                patch = harmonyType.GetMethods().FirstOrDefault(m => m.Name == "Patch" && m.GetParameters().Length >= 2 &&
                                                                    m.GetParameters()[0].ParameterType == typeof(MethodBase));
                Present = patch != null;
                Log.Info($"Harmony {h.GetName().Version} found.");
            }
            catch (Exception e) { Log.Warn("Harmony couldn't be set up: " + e.Message); }
        }

        /// <summary>Put a static prefix (in this assembly) on a method; false if it couldn't.</summary>
        public static bool Prefix(MethodBase original, MethodInfo prefix)
        {
            Init();
            if (!Present || original == null || prefix == null) return false;
            try
            {
                var args = new object[patch.GetParameters().Length];
                args[0] = original;
                args[1] = Activator.CreateInstance(methodType, prefix);
                patch.Invoke(harmony, args);
                Log.Info($"Patched {original.DeclaringType?.Name}.{original.Name}.");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"Couldn't patch {original.DeclaringType?.Name}.{original.Name}: {(e.InnerException ?? e).Message}");
                return false;
            }
        }
    }
}
