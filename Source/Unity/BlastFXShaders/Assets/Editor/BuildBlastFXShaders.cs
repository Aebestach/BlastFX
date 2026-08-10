#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Open this folder as a Unity 2019.4 project (matching KSP 1.12), then use
/// menu BlastFX → Build Shader Bundle.
/// Output: GameData/BlastFX/Shaders/BlastFX.bundle
/// </summary>
public static class BuildBlastFXShaders
{
    // Unity's BuildPipeline always writes lowercase assetBundleName on disk;
    // we rename afterwards so the shipped file is BlastFX.bundle.
    private const string BundleName = "BlastFX";
    private const string ShaderAsset = "Assets/Shaders/Fireball.shader";

    [MenuItem("BlastFX/Build Shader Bundle")]
    public static void Build()
    {
        // Assets → BlastFXShaders → Unity → Source → repo root
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", ".."));
        string outDir = Path.Combine(repoRoot, "GameData", "BlastFX", "Shaders");
        Directory.CreateDirectory(outDir);

        var importer = AssetImporter.GetAtPath(ShaderAsset);
        if (importer == null)
        {
            Debug.LogError("Missing " + ShaderAsset);
            return;
        }

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName + ".bundle",
                assetNames = new[] { ShaderAsset }
            }
        };

        BuildPipeline.BuildAssetBundles(
            outDir,
            builds,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        string desired = Path.Combine(outDir, BundleName + ".bundle");
        string unityLower = Path.Combine(outDir, BundleName.ToLowerInvariant() + ".bundle");
        ForceFileNameCasing(unityLower, desired);

        TryDelete(Path.Combine(outDir, "Shaders"));
        TryDelete(Path.Combine(outDir, "Shaders.manifest"));
        TryDelete(Path.Combine(outDir, BundleName.ToLowerInvariant() + ".bundle.manifest"));
        TryDelete(Path.Combine(outDir, BundleName + ".bundle.manifest"));

        Debug.Log("Built BlastFX shader bundle → " + desired);
    }

    /// <summary>
    /// Windows is case-insensitive, so a single Move won't change casing —
    /// go through a temp name first.
    /// </summary>
    private static void ForceFileNameCasing(string unityPath, string desiredPath)
    {
        string source = null;
        if (File.Exists(unityPath))
        {
            source = unityPath;
        }
        else if (File.Exists(desiredPath))
        {
            source = desiredPath;
        }

        if (source == null)
        {
            Debug.LogWarning("Bundle build finished but no .bundle file was found in " + Path.GetDirectoryName(desiredPath));
            return;
        }

        string temp = desiredPath + ".rename-tmp";
        TryDelete(temp);
        File.Move(source, temp);
        TryDelete(desiredPath);
        File.Move(temp, desiredPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }
}
#endif
