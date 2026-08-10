using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Loads BlastFX/Fireball from BlastFX.bundle.
    /// Shader source lives under Source/Unity/BlastFXShaders — not in GameData.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class BlastShaderLoader : MonoBehaviour
    {
        public const string FireballShaderName = "BlastFX/Fireball";

        private static readonly Dictionary<string, Shader> Shaders =
            new Dictionary<string, Shader>();

        public static bool Ready { get; private set; }

        /// <summary>Resolve the fireball shader.</summary>
        public static Shader FindFireball()
        {
            return Find(FireballShaderName);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LoadBundle();
            Ready = true;
        }

        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Shaders.TryGetValue(name, out Shader shader) && shader != null)
            {
                return shader;
            }

            shader = Shader.Find(name);
            if (shader != null)
            {
                Shaders[name] = shader;
            }

            return shader;
        }

        private static void LoadBundle()
        {
            try
            {
                string root = KSPUtil.ApplicationRootPath + "GameData/BlastFX/Shaders/";
                string bundlePath = Path.Combine(root, "BlastFX.bundle");
                if (!File.Exists(bundlePath))
                {
                    Debug.Log(
                        "[BlastFX] No BlastFX.bundle — fireball FX disabled until you build " +
                        "GameData/BlastFX/Shaders/BlastFX.bundle from Fireball.shader.");
                    return;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogWarning("[BlastFX] Failed to load shader bundle: " + bundlePath);
                    return;
                }

                Shader[] loaded = bundle.LoadAllAssets<Shader>();
                for (int i = 0; i < loaded.Length; i++)
                {
                    Shader s = loaded[i];
                    if (s == null || string.IsNullOrEmpty(s.name))
                    {
                        continue;
                    }

                    Shaders[s.name] = s;
                    Debug.Log("[BlastFX] Loaded shader " + s.name);
                }

                bundle.Unload(false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BlastFX] Shader bundle load error: " + ex.Message);
            }
        }
    }
}
