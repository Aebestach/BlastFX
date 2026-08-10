using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Optional stock Part.explode replacement. Gated by difficulty setting (default off).
    /// Patches both explode() and explode(float) — Harmony needs exact overloads.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class BlastExplodePatchBootstrap : MonoBehaviour
    {
        private static bool applied;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (applied)
            {
                return;
            }

            applied = true;
            try
            {
                var harmony = new Harmony("BlastFX.PartExplode");

                MethodInfo explode0 = AccessTools.Method(typeof(Part), "explode", new System.Type[0]);
                MethodInfo explode1 = AccessTools.Method(typeof(Part), "explode", new[] { typeof(float) });
                MethodInfo prefix = AccessTools.Method(typeof(PartExplodePatch), nameof(PartExplodePatch.Prefix));

                int patched = 0;
                if (explode0 != null)
                {
                    harmony.Patch(explode0, prefix: new HarmonyMethod(prefix));
                    patched++;
                }

                if (explode1 != null)
                {
                    harmony.Patch(explode1, prefix: new HarmonyMethod(prefix));
                    patched++;
                }

                if (patched == 0)
                {
                    Debug.LogWarning("[BlastFX] Could not find Part.explode overloads to patch.");
                }
                else
                {
                    Debug.Log(
                        "[BlastFX] Harmony Part.explode patch registered on " + patched +
                        " overload(s) (active only when setting enabled).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BlastFX] Harmony patch failed: " + ex.Message);
            }
        }
    }

    internal static class PartExplodePatch
    {
        internal static bool Prefix(Part __instance)
        {
            if (BlastPartDestroy.SuppressStockReplace)
            {
                return true;
            }

            if (!BlastSettings.ReplaceStockExplosions)
            {
                return true;
            }

            if (__instance == null || __instance.State == PartStates.DEAD)
            {
                return true;
            }

            // Route through BlastFX fireball; skip stock FX / impulse.
            Blast.DestroyPart(__instance);
            return false;
        }
    }
}
