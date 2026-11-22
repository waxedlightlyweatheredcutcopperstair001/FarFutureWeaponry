using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// At the main menu: find BDArmory, check the other dependencies and say plainly what's missing (in the log and
    /// in one dialog).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Startup : MonoBehaviour
    {
        static readonly (string assembly, string name, string why)[] Required =
        {
            ("BDArmory", "BDArmory or BDArmory Lite", "every weapon is a BDArmory weapon"),
            ("VolumetricFXReconfigured", "VFX Reconfigured", "explosions and antimatter effects"),
            ("PhysicsRangeExtender", "Physics Range Extender", "long-range weapons"),
        };

        void Start()
        {
            Bda.Init();
            var missing = new List<string>();
            foreach (var (assembly, name, why) in Required)
            {
                bool found = false;
                foreach (var la in AssemblyLoader.loadedAssemblies)
                    if (la.name == assembly) { found = true; break; }
                if (found) Log.Info($"{name} found.");
                else
                {
                    Log.Warn($"{name} is not installed ({why}).");
                    missing.Add($"{name} ({why})");
                }
            }
            bool mm = false;
            foreach (var la in AssemblyLoader.loadedAssemblies)
                if (la.name.StartsWith("ModuleManager")) { mm = true; break; }
            if (!mm)
            {
                Log.Warn("ModuleManager is not installed.");
                missing.Add("ModuleManager (patches)");
            }
            Log.Info($"Far Future Weaponry {typeof(Startup).Assembly.GetName().Version} started.");
            if (missing.Count > 0)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "FFWMissing",
                    "Far Future Weaponry", "Far Future Weaponry needs these mods, which aren't installed:\n\n- " +
                    string.Join("\n- ", missing), "OK", false, HighLogic.UISkin);
            }
        }
    }
}
