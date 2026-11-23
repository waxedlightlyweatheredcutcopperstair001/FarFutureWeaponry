using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Far Future Weaponry: builds the plugin's shader bundle (tools/shaders.py runs this in a scratch Unity 2019.4.18f1
// project). Fails with the compiler's message if any shader has an error. Output: FFW_BUNDLE_OUTPUT (the bundle and
// unity-validation.json).
public static class BuildBundle
{
    public const string Name = "farfutureweaponry-windows";
    static readonly string[] Shaders =
    {
        "Assets/Shaders/RustOverlay.shader",
        "Assets/Shaders/ContourWave.shader",
        "Assets/Shaders/PhaseLens.shader",
        "Assets/Shaders/ShieldOverlay.shader",
        "Assets/Shaders/CloakOverlay.shader",
        "Assets/Shaders/Hologram.shader",
    };

    public static void Build()
    {
        try
        {
            if (Application.unityVersion != "2019.4.18f1") throw new Exception("Unity 2019.4.18f1 required (KSP 1.12's).");
            string output = Environment.GetEnvironmentVariable("FFW_BUNDLE_OUTPUT");
            if (string.IsNullOrEmpty(output)) throw new Exception("FFW_BUNDLE_OUTPUT not set.");
            BuildTarget target = BuildTarget.StandaloneWindows64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new Exception("Unity has no Windows standalone build support.");
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.OpenGLCore });
            AssetDatabase.Refresh();
            foreach (string path in Shaders)
            {
                Shader s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s == null) throw new Exception(path + " did not import.");
                foreach (var message in ShaderUtil.GetShaderMessages(s))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        throw new Exception(s.name + ": " + message.message + " (line " + message.line + ")");
                if (ShaderUtil.ShaderHasError(s)) throw new Exception(s.name + " has errors.");
            }
            Directory.CreateDirectory(output);
            var bundle = new AssetBundleBuild { assetBundleName = Name, assetNames = Shaders };
            if (BuildPipeline.BuildAssetBundles(output, new[] { bundle },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression, target) == null)
                throw new Exception("Bundle build failed.");
            File.WriteAllText(Path.Combine(output, "unity-validation.json"), "{\"shaders\": " + Shaders.Length + "}");
            Debug.Log("[FFW] Shader bundle built: " + Shaders.Length + " shaders.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[FFW] " + e.Message);
            EditorApplication.Exit(1);
        }
    }
}
