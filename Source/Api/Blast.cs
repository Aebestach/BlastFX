using System;
using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Public API for other mods. Soft-depend via reflection or a compile reference.
    /// </summary>
    public static class Blast
    {
        /// <summary>True after the shader loader has run (bundle may still be missing).</summary>
        public static bool Ready => BlastShaderLoader.Ready;

        /// <summary>True when the fireball shader is available for drawing.</summary>
        public static bool HasShader => BlastShaderLoader.FindFireball() != null;

        /// <summary>
        /// Recommended fireball size for a part (geometry + remaining fuel / propellant).
        /// </summary>
        public static float SizeForPart(Part part) => BlastSize.ForPart(part);

        /// <summary>Visual-only fireball at a world position.</summary>
        public static void Spawn(Vector3 worldPos, float size = -1f, Color? plasma = null)
        {
            float resolved = size > 0f ? size : 52f;
            FireballImpactFx.Spawn(worldPos, null, false, null, resolved, plasma);
        }

        /// <summary>
        /// Opaque fireball that covers the part, then silently destroys it.
        /// </summary>
        public static void DestroyPart(
            Part part,
            Vector3? worldPos = null,
            float size = -1f,
            Action<bool> onDone = null,
            Color? plasma = null)
        {
            if (part == null)
            {
                onDone?.Invoke(false);
                return;
            }

            Vector3 pos = worldPos ?? (part.partTransform != null
                ? part.partTransform.position
                : part.transform.position);

            FireballImpactFx.Spawn(pos, part, pendingDestroy: true, onDone, size, plasma);
        }

        /// <summary>
        /// Spawn at a part. When <paramref name="destroy"/> is true, deletes the part
        /// once the fireball has covered it.
        /// </summary>
        public static void SpawnAtPart(
            Part part,
            bool destroy,
            float size = -1f,
            Action<bool> onDone = null,
            Color? plasma = null)
        {
            if (part == null)
            {
                onDone?.Invoke(false);
                return;
            }

            Vector3 pos = part.partTransform != null
                ? part.partTransform.position
                : part.transform.position;

            FireballImpactFx.Spawn(pos, part, destroy, onDone, size, plasma);
        }

        /// <summary>
        /// Spawn at an explicit hit point, optionally destroying <paramref name="part"/>
        /// behind the fireball (Thunderbolt strike path).
        /// </summary>
        public static void SpawnAtPoint(
            Vector3 worldPos,
            Part part,
            bool destroy,
            float size = -1f,
            Action<bool> onDone = null,
            Color? plasma = null)
        {
            FireballImpactFx.Spawn(worldPos, part, destroy, onDone, size, plasma);
        }
    }
}
