using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Billboard burst using BlastFX/Fireball.
    /// Nearby simultaneous blasts merge into one so overlapping quads do not show hard seams.
    /// </summary>
    public class FireballImpactFx : MonoBehaviour
    {
        /// <summary>Hide/destroy right after the snap bloom.</summary>
        public const float CoverProgress = 0.06f;

        private const float DefaultDuration = 5f;
        private const float DefaultSize = 42f;
        private const float MinSize = BlastSize.MinSize;
        private const float MaxSize = BlastSize.MaxSize;
        private const float NightBoostMax = 1.85f;
        /// <summary>Merge if centres are closer than this fraction of the larger radius.</summary>
        private const float MergeRadiusFactor = 0.75f;

        private static readonly List<FireballImpactFx> Active = new List<FireballImpactFx>();

        private Material material;
        private GameObject quad;
        private Light flashLight;
        private float life;
        private float duration;
        private float startLightIntensity;
        private float baseSize;
        private float camBias;
        private float nightBoost;

        private readonly List<Part> pendingParts = new List<Part>();
        private bool pendingDestroy;
        private bool coverCommitted;
        private readonly List<Action<bool>> onResolved = new List<Action<bool>>();

        public static void Spawn(
            Vector3 worldPos,
            Part part = null,
            bool pendingDestroy = false,
            Action<bool> onResolved = null,
            float size = -1f,
            Color? plasmaColor = null)
        {
            Shader gpu = BlastShaderLoader.FindFireball();
            if (gpu == null)
            {
                if (pendingDestroy && part != null)
                {
                    BlastPartDestroy.SilentExplode(part);
                    onResolved?.Invoke(true);
                }
                else
                {
                    onResolved?.Invoke(false);
                }

                return;
            }

            float resolvedSize = size > 0f ? size : SizeForPart(part);
            PruneActive();

            // Fold into a nearby live blast instead of stacking billboards.
            for (int i = 0; i < Active.Count; i++)
            {
                FireballImpactFx other = Active[i];
                if (other == null)
                {
                    continue;
                }

                float mergeR = Mathf.Max(other.baseSize, resolvedSize) * MergeRadiusFactor;
                if ((other.transform.position - worldPos).sqrMagnitude <= mergeR * mergeR)
                {
                    other.Absorb(worldPos, part, pendingDestroy, onResolved, resolvedSize);
                    return;
                }
            }

            float night = GetNightBrightnessMultiplier(worldPos, NightBoostMax);
            Color plasma = plasmaColor ?? DefaultPlasmaColor;
            Color hot = Color.Lerp(plasma, new Color(1.9f, 1.95f, 2.2f, 1f), 0.65f);
            Color spark = Color.Lerp(plasma, new Color(1.1f, 1.45f, 2.5f, 1f), 0.5f);

            GameObject root = new GameObject("BlastFX_Impact");
            root.layer = 15;
            root.transform.position = worldPos;

            CelestialBody body = FlightGlobals.currentMainBody;
            if (body != null && body.bodyTransform != null)
            {
                root.transform.parent = body.bodyTransform;
            }

            FireballImpactFx fx = root.AddComponent<FireballImpactFx>();
            if (part != null)
            {
                fx.pendingParts.Add(part);
            }

            fx.pendingDestroy = pendingDestroy && part != null;
            if (onResolved != null)
            {
                fx.onResolved.Add(onResolved);
            }

            fx.Init(gpu, plasma, hot, spark, resolvedSize, night);
            Active.Add(fx);
        }

        public static readonly Color DefaultPlasmaColor = new Color(0.55f, 0.85f, 1.6f, 1f);

        public static float SizeForPart(Part part) => BlastSize.ForPart(part);

        private void Absorb(
            Vector3 worldPos,
            Part part,
            bool destroy,
            Action<bool> callback,
            float size)
        {
            // Pull centre toward the new hit so a cluster reads as one blast.
            transform.position = Vector3.Lerp(transform.position, worldPos, 0.35f);
            baseSize = Mathf.Clamp(Mathf.Max(baseSize, size) * 1.12f, MinSize, MaxSize);

            if (part != null && !pendingParts.Contains(part))
            {
                pendingParts.Add(part);
            }

            pendingDestroy = pendingDestroy || destroy;
            if (callback != null)
            {
                onResolved.Add(callback);
            }

            // Retrigger bloom a bit so a chain reaction feels alive.
            float progress = 1f - Mathf.Clamp01(life / duration);
            if (progress > 0.15f)
            {
                life = Mathf.Max(life, duration * 0.88f);
            }

            if (coverCommitted && destroy && part != null && part.State != PartStates.DEAD)
            {
                // Already past cover — destroy newly absorbed parts immediately (hidden).
                BlastPartDestroy.HideRenderers(part);
                BlastPartDestroy.SilentExplode(part);
            }
            else if (coverCommitted && part != null)
            {
                BlastPartDestroy.HideRenderers(part);
            }

            if (quad != null)
            {
                float s = baseSize;
                quad.transform.localScale = new Vector3(s, s, 1f);
            }

            if (flashLight != null)
            {
                flashLight.range = baseSize * 20f;
                flashLight.intensity = Mathf.Max(flashLight.intensity, startLightIntensity * 0.85f);
            }
        }

        private void Init(
            Shader gpu,
            Color plasma,
            Color hot,
            Color spark,
            float size,
            float night)
        {
            duration = DefaultDuration;
            life = duration;
            baseSize = size;
            nightBoost = night;
            // Tiny shared bias — large per-blast bias made overlapping quads cut each other.
            camBias = 0.35f;

            material = new Material(gpu);
            material.SetColor("_Color", plasma);
            material.SetColor("_HotColor", hot);
            material.SetColor("_SparkColor", spark);
            material.SetColor("_FireColor", new Color(1.45f, 0.42f, 0.06f, 1f));
            material.SetColor("_FireHotColor", new Color(1.95f, 1.4f, 0.32f, 1f));
            material.SetColor("_SmokeColor", new Color(0.07f, 0.06f, 0.05f, 1f));
            material.SetFloat("_Progress", 0f);
            material.SetFloat("_Seed", UnityEngine.Random.Range(0f, 1000f));
            material.SetFloat("_Intensity", 9f * nightBoost);
            material.SetFloat("_RingCount", 2.5f);
            material.SetFloat("_SparkAmount", 1.2f);
            material.SetFloat("_Turbulence", UnityEngine.Random.Range(1.05f, 1.55f));
            material.SetFloat("_FireAmount", UnityEngine.Random.Range(1.25f, 1.55f));
            material.renderQueue = 3000;

            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider col = quad.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            quad.name = "BlastFX_ImpactQuad";
            quad.layer = 15;
            quad.transform.parent = transform;
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localScale = new Vector3(size, size, 1f);

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            flashLight = gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.62f, 0.28f, 1f);
            startLightIntensity = 7.5f * nightBoost;
            flashLight.intensity = startLightIntensity;
            flashLight.range = size * 20f;
            flashLight.cullingMask = ~0;

            FaceCamera();
        }

        private void Update()
        {
            float dt = TimeWarp.deltaTime > 0f ? TimeWarp.deltaTime : Time.deltaTime;
            life -= dt;
            float progress = 1f - Mathf.Clamp01(life / duration);

            if (material != null)
            {
                material.SetFloat("_Progress", progress);
            }

            if (flashLight != null)
            {
                float lightT = Mathf.Exp(-progress * 3.2f) * 0.85f
                    + 0.35f * (1f - Mathf.SmoothStep(0.45f, 1f, progress));
                float firePulse = Mathf.Sin(progress * 12f) * 0.07f + 0.93f;
                flashLight.intensity = startLightIntensity * Mathf.Max(0f, lightT) * firePulse;
                flashLight.color = Color.Lerp(
                    new Color(0.85f, 0.9f, 1.15f, 1f),
                    new Color(1f, 0.4f, 0.12f, 1f),
                    Mathf.Clamp01(progress * 1.4f));
            }

            if (quad != null)
            {
                float bloom = Mathf.Clamp01(progress / 0.045f);
                float swell = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.04f) / 0.55f));
                float settle = 1f - 0.12f * Mathf.SmoothStep(0.55f, 1f, progress);
                float grow = Mathf.Lerp(0.92f, 1.08f, bloom) + 0.28f * swell;
                float s = baseSize * grow * settle;
                quad.transform.localScale = new Vector3(s, s, 1f);
            }

            FaceCamera();

            if (!coverCommitted && progress >= CoverProgress)
            {
                CommitCover();
            }

            if (life <= 0f)
            {
                if (!coverCommitted)
                {
                    CommitCover();
                }

                Cleanup();
                Destroy(gameObject);
            }
        }

        private void CommitCover()
        {
            if (coverCommitted)
            {
                return;
            }

            coverCommitted = true;

            bool didDestroy = false;
            for (int i = 0; i < pendingParts.Count; i++)
            {
                Part part = pendingParts[i];
                if (part == null || part.State == PartStates.DEAD)
                {
                    continue;
                }

                BlastPartDestroy.HideRenderers(part);
                if (pendingDestroy)
                {
                    didDestroy = BlastPartDestroy.SilentExplode(part) || didDestroy;
                }
            }

            for (int i = 0; i < onResolved.Count; i++)
            {
                onResolved[i]?.Invoke(didDestroy);
            }

            onResolved.Clear();
        }

        private void FaceCamera()
        {
            if (quad == null || FlightCamera.fetch == null)
            {
                return;
            }

            Transform cam = FlightCamera.fetch.transform;
            Vector3 toCam = cam.position - transform.position;
            if (toCam.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector3 towardCam = toCam.normalized;
            // Keep all blasts on nearly the same camera plane to avoid cross-cutting.
            quad.transform.position = transform.position + towardCam * camBias;

            // Lock "up" to the planet (not camera up) so orbiting the view does not
            // spin the fireball pattern — only yaw to face the camera.
            Vector3 up = Vector3.up;
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body != null)
            {
                Vector3 radial = transform.position - body.position;
                if (radial.sqrMagnitude > 1e-6f)
                {
                    up = radial.normalized;
                }
            }

            Vector3 face = -towardCam;
            Vector3 right = Vector3.Cross(up, face);
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.Cross(cam.up, face);
            }

            right.Normalize();
            Vector3 lockedUp = Vector3.Cross(face, right).normalized;
            quad.transform.rotation = Quaternion.LookRotation(face, lockedUp);
        }

        private void Cleanup()
        {
            Active.Remove(this);

            if (material != null)
            {
                Destroy(material);
                material = null;
            }

            if (quad != null)
            {
                Destroy(quad);
                quad = null;
            }
        }

        private void OnDestroy()
        {
            Active.Remove(this);

            if (!coverCommitted)
            {
                if (pendingDestroy)
                {
                    for (int i = 0; i < pendingParts.Count; i++)
                    {
                        Part part = pendingParts[i];
                        if (part != null && part.State != PartStates.DEAD)
                        {
                            BlastPartDestroy.SilentExplode(part);
                        }
                    }
                }

                for (int i = 0; i < onResolved.Count; i++)
                {
                    onResolved[i]?.Invoke(pendingDestroy);
                }

                onResolved.Clear();
            }

            Cleanup();
        }

        private static void PruneActive()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] == null)
                {
                    Active.RemoveAt(i);
                }
            }
        }

        private static float GetNightBrightnessMultiplier(Vector3 worldPos, float nightMultiplier)
        {
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body == null || Sun.Instance == null || Sun.Instance.sun == null)
            {
                return 1f;
            }

            Vector3 up = (worldPos - body.position).normalized;
            Vector3 toSun = (Sun.Instance.sun.position - body.position).normalized;
            float sunElevation = Vector3.Dot(up, toSun);
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, -0.08f, sunElevation));
            return Mathf.Lerp(1f, nightMultiplier, night);
        }
    }
}
