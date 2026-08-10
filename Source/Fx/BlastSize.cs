using UnityEngine;

namespace BlastFX
{
    /// <summary>
    /// Estimates fireball world size from part geometry + remaining explosive resources.
    /// Empty tanks pop smaller; full LF/Ox / SRB loads bloom much larger.
    /// </summary>
    public static class BlastSize
    {
        public const float MinSize = 10f;
        public const float MaxSize = 160f;

        /// <summary>
        /// World-space quad size (metres-ish) for a blast centred on this part.
        /// </summary>
        public static float ForPart(Part part)
        {
            if (part == null)
            {
                return 42f;
            }

            try
            {
                float geom = GeometricSpan(part);
                // Structural baseline from physical size.
                float geomSize = Mathf.Clamp(geom * 2.15f + 6f, 12f, 95f);

                GetExplosiveLoad(part, out float explosiveMass, out float amountSum, out float capacitySum);

                float fuelMul = FuelMultiplier(explosiveMass, amountSum, capacitySum);
                float typeMul = TypeMultiplier(part);
                float potMul = PotentialMultiplier(part);

                float size = geomSize * fuelMul * typeMul * potMul;
                return Mathf.Clamp(size, MinSize, MaxSize);
            }
            catch
            {
                return 42f;
            }
        }

        private static float GeometricSpan(Part part)
        {
            float span = 0f;
            Bounds[] bounds = part.GetRendererBounds();
            if (bounds != null)
            {
                for (int i = 0; i < bounds.Length; i++)
                {
                    span = Mathf.Max(span, bounds[i].size.magnitude);
                }
            }

            if (span < 0.35f && part.collider != null)
            {
                span = part.collider.bounds.size.magnitude;
            }

            if (span < 0.2f)
            {
                // Tiny part fallback from dry mass.
                span = Mathf.Clamp(part.mass * 2.5f, 0.25f, 4f);
            }

            return span;
        }

        private static void GetExplosiveLoad(
            Part part,
            out float explosiveMass,
            out float amountSum,
            out float capacitySum)
        {
            explosiveMass = 0f;
            amountSum = 0f;
            capacitySum = 0f;

            if (part.Resources == null)
            {
                return;
            }

            for (int i = 0; i < part.Resources.Count; i++)
            {
                PartResource res = part.Resources[i];
                if (res == null || res.info == null)
                {
                    continue;
                }

                float weight = ExplosiveWeight(res.info.name);
                if (weight <= 0f)
                {
                    continue;
                }

                float amount = (float)res.amount;
                float max = (float)res.maxAmount;
                float density = Mathf.Max(0f, res.info.density);

                amountSum += amount * weight;
                capacitySum += max * weight;

                // Mass term (dense storables) + volume floor (LH2 / cryogens are light but bulky).
                float massTerm = amount * density * weight;
                float volumeTerm = amount * 0.0004f * weight;
                explosiveMass += Mathf.Max(massTerm, volumeTerm);
            }
        }

        /// <summary>
        /// Relative VFX contribution (not real TNT). Covers stock, CRP, CryoTanks, RO/RealFuels.
        /// Low-density cryogens (LH2) get higher weights so volume still reads as a big blast.
        /// </summary>
        private static float ExplosiveWeight(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return 0f;
            }

            switch (resourceName)
            {
                // --- Stock ---
                case "LiquidFuel":
                    return 1f;
                case "Oxidizer":
                    return 1.05f;
                case "SolidFuel":
                    return 1.25f;
                case "MonoPropellant":
                    return 0.85f;
                case "XenonGas":
                    return 0.12f;

                // --- CryoTanks / CRP cryogens (RO hydrolox, methalox, etc.) ---
                case "LqdHydrogen":
                case "LiquidHydrogen":
                case "SolidHydrogen":
                    return 2.4f; // very low density — boost so full cryo tanks bloom
                case "LqdOxygen":
                case "LiquidOxygen":
                case "LqdOxygen18":
                    return 1.15f;
                case "LqdMethane":
                case "LiquidMethane":
                case "Methane":
                    return 1.2f;
                case "LqdAmmonia":
                case "Ammonia":
                    return 0.9f;
                case "LqdFluorine":
                case "Fluorine":
                    return 1.35f;
                case "LqdCO":
                case "CarbonMonoxide":
                    return 0.55f;
                case "Ethane":
                case "Ethylene":
                case "Ethanol":
                case "Ethanol75":
                case "Ethanol90":
                case "Methanol":
                    return 0.95f;

                // --- RO / RealFuels hypergolics & storable ---
                case "Aerozine50":
                case "UDMH":
                case "UH25":
                case "MMH":
                case "Hydrazine":
                case "Hydyne":
                case "Aniline":
                case "Furfuryl":
                case "Tonka250":
                case "Tonka500":
                case "CaveaB":
                    return 1.05f;
                case "NTO":
                case "N2O4":
                case "MON1":
                case "MON3":
                case "MON10":
                case "MON15":
                case "MON20":
                case "MON25":
                case "IRFNA-III":
                case "IRFNA-IV":
                case "IWFNA":
                case "AK20":
                case "AK27":
                case "NitrousOxide":
                    return 1.1f;
                case "HTP":
                case "H2O2":
                    return 0.95f;
                case "Kerosene":
                case "Syntin":
                case "AvGas":
                    return 1f;

                // --- High-energy / fluorine mixes (RO) ---
                case "ClF3":
                case "ClF5":
                case "N2F4":
                case "OF2":
                case "FLOX30":
                case "FLOX70":
                case "FLOX88":
                case "Diborane":
                case "Pentaborane":
                case "Decaborane":
                case "Hexaborane":
                    return 1.4f;

                // --- Solids (RO / CRP) ---
                case "HTPB":
                case "PBAN":
                case "PSPC":
                case "HNIW":
                case "NGNC":
                    return 1.3f;

                // --- Electric / cold-gas (weak VFX) ---
                case "ArgonGas":
                case "LqdArgon":
                case "KryptonGas":
                case "LqdKrypton":
                case "NeonGas":
                case "LqdNeon":
                case "LqdXenon":
                case "Nitrogen":
                case "LqdNitrogen":
                case "LqdNitrogen15":
                case "Helium":
                case "LqdHelium":
                case "Helium3":
                case "Helium4":
                case "LqdHe3":
                case "Hydrogen": // gaseous H2 bottle, not a cryo tank load
                    return 0.1f;

                // --- Nuclear / exotic (rare, huge if present) ---
                case "Antimatter":
                case "AntiHydrogen":
                    return 8f;
                case "FusionPellets":
                    return 1.5f;

                // --- Explicit non-propellant ---
                case "IntakeAir":
                case "IntakeAtm":
                case "IntakeLqd":
                case "ElectricCharge":
                case "StoredCharge":
                case "Megajoules":
                case "ThermalPower":
                case "WasteHeat":
                case "Ore":
                case "Ablator":
                case "LeadBallast":
                case "Water":
                case "WasteWater":
                case "Food":
                case "Supplies":
                case "Oxygen": // life-support gas, not LOX tank
                case "CarbonDioxide":
                case "LqdCO2":
                case "TEATEB": // ignitor slug, tiny
                    return 0f;
            }

            // Fuzzy fallback for other RF/RO patches / rename packs.
            string n = resourceName.ToLowerInvariant();
            if (n.Contains("ballast") || n.Contains("waste") || n.Contains("ore")
                || n.Contains("water") || n.Contains("food") || n.Contains("charge")
                || n.Contains("ablat") || n.Contains("intake"))
            {
                return 0f;
            }

            if (n.Contains("antimatter") || n.Contains("antihydrogen"))
            {
                return 8f;
            }

            if (n.Contains("lqdhitrogen") || n.Contains("lqdhelium") || n.Contains("lqdneon")
                || n.Contains("lqdargon") || n.Contains("lqdxenon") || n.Contains("lqdkrypton"))
            {
                return 0.1f;
            }

            if (n.Contains("lqdhydrogen") || n.Contains("liquidhydrogen") || n.Contains("slush"))
            {
                return 2.4f;
            }

            if (n.Contains("lqdoxygen") || n.Contains("liquidoxygen") || n.Contains("lox"))
            {
                return 1.15f;
            }

            if (n.Contains("methane") || n.Contains("kerosene") || n.Contains("rp-1") || n.Contains("rp1")
                || n.Contains("syntin") || n.Contains("avgas") || n.Contains("liquidfuel"))
            {
                return 1f;
            }

            if (n.Contains("udmh") || n.Contains("mmh") || n.Contains("hydrazine")
                || n.Contains("aerozine") || n.Contains("hypergolic") || n.Contains("tonka")
                || n.Contains("aniline") || n.Contains("hydyne"))
            {
                return 1.05f;
            }

            if (n.Contains("nto") || n.Contains("n2o4") || n.Contains("mon") || n.Contains("irfna")
                || n.Contains("nitrous") || n.Contains("oxidizer") || n.Contains("oxygen"))
            {
                // Avoid matching life-support "Oxygen" (exact case handled above).
                if (n == "oxygen")
                {
                    return 0f;
                }

                return 1.08f;
            }

            if (n.Contains("htpb") || n.Contains("pban") || n.Contains("pspc") || n.Contains("solid"))
            {
                return 1.25f;
            }

            if (n.Contains("clf3") || n.Contains("clf5") || n.Contains("fluorine") || n.Contains("flox")
                || n.Contains("diborane") || n.Contains("pentaborane") || n.Contains("borane"))
            {
                return 1.35f;
            }

            if (n.Contains("htp") || n.Contains("peroxide") || n.Contains("mono"))
            {
                return 0.9f;
            }

            if (n.Contains("xenon") || n.Contains("argon") || n.Contains("krypton") || n.Contains("neon"))
            {
                return 0.12f;
            }

            // Unknown "fuel-like" name — mild contribution rather than ignore.
            if (n.Contains("fuel") || n.Contains("propellant") || n.Contains("lqd"))
            {
                return 0.75f;
            }

            return 0f;
        }

        private static float FuelMultiplier(float explosiveMass, float amountSum, float capacitySum)
        {
            // No energetic propellant left — small structural / electrical pop.
            if (explosiveMass < 0.0005f && amountSum < 0.5f)
            {
                return 0.52f;
            }

            // Log curve: ~0.05 t mild, ~1 t noticeable, ~8 t+ jumbo-class.
            float wetBoost = 1f + Mathf.Clamp(Mathf.Log10(1f + explosiveMass * 10f), 0f, 1.9f);

            if (capacitySum < 1f)
            {
                return wetBoost;
            }

            // Empty vs full tank of the same part.
            float fill = Mathf.Clamp01(amountSum / capacitySum);
            float emptyMul = 0.55f;
            // Slightly sub-linear so half-full is closer to "still juicy".
            return Mathf.Lerp(emptyMul, wetBoost, Mathf.Pow(fill, 0.7f));
        }

        private static float TypeMultiplier(Part part)
        {
            float mul = 1f;

            if (part.FindModuleImplementing<ModuleEngines>() != null
                || part.FindModuleImplementing<ModuleEnginesFX>() != null)
            {
                mul *= 1.12f;
            }

            // Very light / tiny probe bits stay compact.
            if (part.mass < 0.08f && GeometricSpan(part) < 1.2f)
            {
                mul *= 0.7f;
            }
            else if (part.mass < 0.25f && GeometricSpan(part) < 2f)
            {
                mul *= 0.85f;
            }

            return mul;
        }

        private static float PotentialMultiplier(Part part)
        {
            try
            {
                // Stock authoring hint when present (often ~0–1).
                float p = part.explosionPotential;
                return Mathf.Lerp(0.88f, 1.4f, Mathf.Clamp01(p));
            }
            catch
            {
                return 1f;
            }
        }
    }
}
