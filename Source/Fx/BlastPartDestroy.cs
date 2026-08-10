using System.Collections.Generic;
using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Hide part meshes then explode with no stock fireball (BlastFX replaces it).
    /// </summary>
    public static class BlastPartDestroy
    {
        /// <summary>
        /// When true, Harmony Part.explode prefix must not re-enter Blast.DestroyPart.
        /// </summary>
        public static bool SuppressStockReplace { get; private set; }

        public static void HideRenderers(Part part)
        {
            if (part == null)
            {
                return;
            }

            var renderers = part.FindModelComponents<Renderer>();
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }
        }

        public static bool SilentExplode(Part part)
        {
            if (part == null || part.State == PartStates.DEAD)
            {
                return false;
            }

            Vector3 origin = part.partTransform != null
                ? part.partTransform.position
                : part.transform.position;

            HideRenderers(part);

            float saved = part.explosionPotential;
            part.explosionPotential = 0f;
            SuppressStockReplace = true;
            try
            {
                part.explode();
            }
            finally
            {
                SuppressStockReplace = false;
                try
                {
                    if (part != null)
                    {
                        part.explosionPotential = saved;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            DampNearbyDebris(origin, radius: 80f, velocityScale: 0.38f, maxSpeed: 28f);
            return true;
        }

        private static void DampNearbyDebris(Vector3 origin, float radius, float velocityScale, float maxSpeed)
        {
            if (FlightGlobals.Vessels == null)
            {
                return;
            }

            float r2 = radius * radius;
            for (int i = 0; i < FlightGlobals.Vessels.Count; i++)
            {
                Vessel vessel = FlightGlobals.Vessels[i];
                if (vessel == null || !vessel.loaded)
                {
                    continue;
                }

                if (vessel.vesselType != VesselType.Debris)
                {
                    continue;
                }

                if ((vessel.GetWorldPos3D() - origin).sqrMagnitude > r2)
                {
                    continue;
                }

                List<Part> parts = vessel.parts;
                if (parts == null)
                {
                    continue;
                }

                for (int p = 0; p < parts.Count; p++)
                {
                    Part part = parts[p];
                    Rigidbody rb = part != null ? part.Rigidbody : null;
                    if (rb == null)
                    {
                        continue;
                    }

                    Vector3 v = rb.velocity * velocityScale;
                    if (v.sqrMagnitude > maxSpeed * maxSpeed)
                    {
                        v = v.normalized * maxSpeed;
                    }

                    rb.velocity = v;
                    rb.angularVelocity *= velocityScale;
                }
            }
        }
    }
}
