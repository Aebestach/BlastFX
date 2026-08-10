Shader "BlastFX/Fireball"
{
	// Billboard quad (UV 0-1). Drive _Progress 0-1 from C#.
	// Pass 0: opaque fireball (hides the part) + Pass 1: additive electric overlay.
	Properties
	{
		_Color ("Plasma Color", Color) = (0.45, 0.75, 1.6, 1)
		_HotColor ("Core Hot Color", Color) = (1.8, 1.9, 2.2, 1)
		_SparkColor ("Spark Color", Color) = (1.2, 1.5, 2.4, 1)
		_FireColor ("Fire Color", Color) = (1.4, 0.45, 0.08, 1)
		_FireHotColor ("Fire Hot Color", Color) = (1.9, 1.35, 0.35, 1)
		_SmokeColor ("Smoke Color", Color) = (0.08, 0.07, 0.06, 1)
		_Progress ("Progress", Range(0, 1)) = 0
		_Seed ("Seed", Float) = 0
		_Intensity ("Intensity", Float) = 10
		_RingCount ("Ring Count", Range(1, 4)) = 2.5
		_SparkAmount ("Spark Amount", Range(0, 2)) = 1.25
		_Turbulence ("Turbulence", Range(0, 2)) = 1.0
		_FireAmount ("Fire Amount", Range(0, 2)) = 1.35
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	float4 _Color;
	float4 _HotColor;
	float4 _SparkColor;
	float4 _FireColor;
	float4 _FireHotColor;
	float4 _SmokeColor;
	float _Progress;
	float _Seed;
	float _Intensity;
	float _RingCount;
	float _SparkAmount;
	float _Turbulence;
	float _FireAmount;

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	float Hash11(float p)
	{
		p = frac(p * 0.1031);
		p *= p + 33.33;
		p *= p + p;
		return frac(p);
	}

	float Hash21(float2 p)
	{
		float3 p3 = frac(float3(p.xyx) * 0.1031);
		p3 += dot(p3, p3.yzx + 33.33);
		return frac((p3.x + p3.y) * p3.z);
	}

	float Noise2D(float2 p)
	{
		float2 i = floor(p);
		float2 f = frac(p);
		float a = Hash21(i);
		float b = Hash21(i + float2(1, 0));
		float c = Hash21(i + float2(0, 1));
		float d = Hash21(i + float2(1, 1));
		float2 u = f * f * (3.0 - 2.0 * f);
		return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 2.0 - 1.0;
	}

	float Fbm(float2 p)
	{
		float v = 0.0;
		float a = 0.5;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			v += Noise2D(p) * a;
			p = p * 2.07 + float2(17.1, 9.3);
			a *= 0.5;
		}
		return v;
	}

	float SoftDisk(float r, float radius, float soft)
	{
		return saturate(1.0 - smoothstep(radius - soft, radius + soft, r));
	}

	float ShockRing(float r, float radius, float width)
	{
		float d = abs(r - radius);
		return saturate(1.0 - d / max(1e-4, width));
	}

	// Angle-continuous FBM ù never feed raw atan2 into noise (that leaves a seam).
	float FbmPolar(float2 p, float radial, float angScale, float seed)
	{
		float2 dir = normalize(p + 1e-5);
		return Fbm(dir * angScale + float2(radial, seed));
	}

	float NoisePolar(float2 p, float radial, float angScale, float seed)
	{
		float2 dir = normalize(p + 1e-5);
		return Noise2D(dir * angScale + float2(radial, seed));
	}

	float SparkRays(float2 p, float r, float t, float seed)
	{
		// Wrap-safe angle in [0,1); evaluate both sides near 0/1 so cells meet.
		float ang01 = atan2(p.y, p.x) / 6.2831853 + 0.5;
		float rays = 0.0;
		float dens = 10.0 + 6.0 * _SparkAmount;

		[unroll]
		for (int k = 0; k < 3; k++)
		{
			float layer = (float)k;
			float phase = layer * 1.7 + seed;
			float u = ang01 * dens + phase;
			float cell = floor(u);
			float local = frac(u);
			// Distance to nearest cell centre, wrapping across the 0/1 cut.
			float centred = min(abs(local - 0.5), min(local + 0.5, 1.5 - local));
			centred = centred * 2.0;

			float h = Hash11(cell + seed * (3.1 + layer));
			// Also hash the wrapped neighbour so the cut cell matches.
			float cellW = floor(frac(ang01 + 1e-4) * dens + phase);
			float hW = Hash11(cellW + seed * (3.1 + layer));
			float seamW = smoothstep(0.08, 0.0, min(ang01, 1.0 - ang01));
			h = lerp(h, hW, seamW);

			float jag = NoisePolar(p, r * 8.0 + layer * 5.0, 3.0, cell) * 0.35;
			float halfW = 0.08 + 0.12 * h + jag * 0.08;
			float beam = saturate(1.0 - centred / max(1e-3, halfW));
			beam = beam * beam;

			// Shorter forks ù local blast, not long streamers.
			float len = 0.18 + 0.42 * Hash11(cell + 41.0 + seed);
			float reach = lerp(0.12, 0.72, saturate(t * 0.95 + 0.05 * h));
			float along = smoothstep(0.02, 0.10, r) * smoothstep(len * reach, len * reach * 0.5, r);

			float flicker = 0.65 + 0.35 * Hash11(floor(_Time.y * 36.0) + cell + seed);
			float alive = step(0.22, h);
			rays += beam * along * flicker * alive * (1.0 - 0.35 * layer);
		}

		return rays * _SparkAmount;
	}

	float EmberSparks(float2 p, float t, float seed)
	{
		float sum = 0.0;
		[unroll]
		for (int i = 0; i < 14; i++)
		{
			float id = (float)i + seed * 7.13;
			float a = Hash11(id) * 6.2831853;
			// Slow, short-travel embers ù?more deflagration than shrapnel.
			float speed = 0.22 + 0.32 * Hash11(id + 19.0);
			float birth = Hash11(id + 3.7) * 0.18;
			float age = saturate((t - birth) / max(1e-3, 1.0 - birth));
			float dist = age * speed * (0.45 + 0.28 * Hash11(id + 8.0));
			float2 pos = float2(cos(a), sin(a)) * dist;
			float swirl = age * 1.1 * (Hash11(id + 11.0) * 2.0 - 1.0);
			float cs = cos(swirl);
			float sn = sin(swirl);
			pos = float2(pos.x * cs - pos.y * sn, pos.x * sn + pos.y * cs);

			float d = length(p - pos);
			float size = lerp(0.055, 0.016, age) * (0.7 + 0.6 * Hash11(id + 29.0));
			float glow = saturate(1.0 - d / size);
			glow = glow * glow;
			float life = smoothstep(0.0, 0.10, age) * smoothstep(1.0, 0.78, age);
			sum += glow * life;
		}
		return sum;
	}

	v2f VertCommon(appdata v)
	{
		v2f o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		return o;
	}

	// Snap bloom (~0.2s of a 5s life), then slow climb.
	float FireballRadius(float t)
	{
		float bloom = saturate(t / 0.045);
		float climb = saturate((t - 0.03) / 0.97);
		return lerp(0.22, 0.55, pow(bloom, 0.45)) + 0.34 * pow(climb, 0.85);
	}

	// Seed-stable random in [lo, hi].
	float SeedRange(float salt, float lo, float hi)
	{
		return lerp(lo, hi, Hash11(_Seed * 1.718 + salt));
	}

	// Break the perfect circle. `down` is 0 at top / 1 toward ground (billboard -Y).
	float RaggedRadius(float2 pw, float t, float baseR, float down)
	{
		float f1 = SeedRange(1.1, 2.4, 4.6);
		float f2 = SeedRange(2.3, 5.5, 9.5);
		float f3 = SeedRange(3.7, 10.0, 16.0);
		float w1 = SeedRange(4.2, 0.28, 0.48);
		float w2 = SeedRange(5.5, 0.14, 0.28);
		float w3 = SeedRange(6.8, 0.08, 0.18);

		float lobes = FbmPolar(pw, t * SeedRange(7.1, 1.2, 2.2), f1, _Seed + 2.1);
		float fine = FbmPolar(pw, t * SeedRange(8.4, 2.0, 3.6) + 4.0, f2, _Seed + 8.4);
		float nicks = FbmPolar(pw, t * 3.5 + 9.0, f3, _Seed + 15.2);

		float ragged = lobes * w1 + fine * w2 + nicks * w3;
		float bite = pow(saturate(-lobes * SeedRange(9.0, 0.4, 0.75) + 0.4), SeedRange(10.2, 1.6, 2.6));
		ragged -= bite * SeedRange(11.5, 0.18, 0.34);

		float cleftAng = SeedRange(12.8, 0.0, 6.2831853);
		float2 cleftDir = float2(cos(cleftAng), sin(cleftAng));
		float cleft = saturate(dot(normalize(pw + 1e-5), cleftDir));
		cleft = pow(cleft, SeedRange(13.9, 4.0, 10.0));
		ragged -= cleft * SeedRange(14.3, 0.06, 0.20);

		// Ground-facing edge: chew hard so it is not a flat chord / straight line.
		float bottomW = smoothstep(0.05, 0.85, down);
		float chew = Fbm(float2(pw.x * 7.5 + _Seed * 0.3, t * 2.2));
		float chew2 = Fbm(float2(pw.x * 18.0 - _Seed, t * 3.8 + 5.0));
		ragged -= bottomW * (0.16 + chew * 0.38 + chew2 * 0.22);
		// Hanging drips / scallops ù some spans extend downward unevenly.
		float drip = pow(saturate(chew * 0.5 + 0.5), 2.4);
		ragged += bottomW * drip * 0.32;
		// Break left-right symmetry along the bottom.
		ragged += bottomW * sin(pw.x * 9.0 + _Seed) * 0.08;

		return max(0.04, baseR * (1.0 + ragged));
	}

	// Fast pop-in, long soft dissolve ù avoids "instant vanish" at the end.
	float LifeEnvelope(float t)
	{
		float appear = smoothstep(0.0, 0.03, t);
		// Primary fade starts mid-late; extra soft tail to 1.0.
		float fade = 1.0 - smoothstep(0.48, 0.92, t);
		float tail = 1.0 - smoothstep(0.88, 1.0, t);
		return appear * fade * lerp(1.0, tail, 0.65);
	}

	void SampleDomain(float2 uv, float t, out float2 pw, out float rw, out float diskMask)
	{
		float2 p = uv * 2.0 - 1.0;
		float r = length(p);
		diskMask = SoftDisk(r, 0.995, 0.04);

		// Mild yaw only ù keep billboard -Y as "down" so the ground edge stays the torn side.
		float rot = SeedRange(20.1, -0.55, 0.55);
		float cs = cos(rot);
		float sn = sin(rot);
		float2 pr = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
		float stretchX = SeedRange(21.4, 0.78, 1.22);
		// Slightly taller than wide so the bottom read is less of a flat ellipse chord.
		float stretchY = SeedRange(22.7, 0.95, 1.35);
		pr *= float2(stretchX, stretchY);

		float warpAmt = SeedRange(23.5, 0.10, 0.18) * _Turbulence;
		float warp =
			Fbm(pr * SeedRange(24.2, 2.0, 3.4) + _Seed + float2(t * 1.1, -t * 0.9)) *
			warpAmt *
			(1.0 - t * 0.35);
		float2 dir = normalize(pr + 1e-4);
		float shoveAmt = SeedRange(25.8, 0.05, 0.12);
		float2 shove = float2(
			Fbm(pr * 1.7 + _Seed + 3.0),
			Fbm(pr.yx * 1.7 + _Seed + 7.0)) * shoveAmt * (1.0 - t * 0.4);
		// Slow drift of the whole mass, unique per seed.
		float2 drift = float2(
			SeedRange(26.1, -1.0, 1.0),
			SeedRange(27.4, -1.0, 1.0)) * (0.04 + 0.06 * t);

		pw = pr + dir * warp + shove + drift;
		rw = length(pw);
	}
	ENDCG

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
		}

		// ---- Pass 0: fireball body (ZWrite off so stacked blasts do not cut seams) ----
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			v2f vert(appdata v) { return VertCommon(v); }

			float4 frag(v2f i) : SV_Target
			{
				float t = saturate(_Progress);
				float2 pw;
				float rw;
				float diskMask;
				SampleDomain(i.uv, t, pw, rw, diskMask);
				if (diskMask <= 0.001)
				{
					clip(-1);
					return 0;
				}

				// Billboard -Y faces the ground with our locked-up orientation.
				float down = saturate(-(i.uv.y * 2.0 - 1.0));
				float ballR = RaggedRadius(pw, t, FireballRadius(t), down);
				// Soften more on the bottom so the cut is not a hard ruler edge.
				float edgeSoft = 0.018 + 0.02 * t + down * 0.04;
				float body = SoftDisk(rw, ballR, edgeSoft);
				float core = SoftDisk(rw, ballR * 0.42, 0.07);
				float rim = SoftDisk(rw, ballR, edgeSoft * 3.0) - SoftDisk(rw, ballR * 0.90, edgeSoft * 2.2);
				rim = saturate(rim);

				float boil = 0.55 + 0.45 * Fbm(pw * SeedRange(30.2, 3.4, 5.2) + float2(_Seed * 0.2, t * 1.6));
				float tongues = abs(FbmPolar(pw, rw * 4.8 - t * 2.4, SeedRange(31.5, 2.2, 3.8), _Seed));
				tongues = pow(saturate(1.0 - tongues * 1.45), 2.1);

				// Extra downward fire curtains / fingers along the ground edge.
				float fingerNoise = FbmPolar(pw, rw * 3.2 - t * 2.0, SeedRange(32.8, 4.0, 7.5), _Seed + 5.0);
				float fingerReach = SeedRange(33.6, 1.08, 1.35) + down * 0.22;
				float fingers = SoftDisk(rw, ballR * (fingerReach + fingerNoise * 0.28), 0.08)
					* saturate(fingerNoise * SeedRange(34.1, 1.1, 1.8))
					* smoothstep(ballR * 0.45, ballR * 0.95, rw);
				float bottomLick = SoftDisk(rw, ballR * (1.05 + fingerNoise * 0.35), 0.1)
					* down
					* saturate(0.3 + fingerNoise);
				fingers = max(fingers, bottomLick);

				// Late breakup: holes / smoke voids so it dies as chunks, not a shrinking ball.
				float breakup = smoothstep(0.40, 0.90, t);
				float voids = Fbm(pw * 2.8 + float2(_Seed, t * 1.3));
				float solid = lerp(1.0, saturate(voids * 1.7 + 0.15 + tongues * 0.25), breakup);

				float lifeEnv = LifeEnvelope(t);
				float alpha = saturate((body + fingers * 0.85) * lifeEnv * diskMask * solid);
				alpha = max(alpha, core * lifeEnv * diskMask * lerp(1.0, solid, 0.5));
				// Hard clip only while opaque; ease the threshold so the end can soft-fade.
				float clipThr = lerp(0.30, 0.02, smoothstep(0.45, 0.85, t));
				clip(alpha - clipThr);
				alpha = saturate(alpha * _FireAmount);

				// --- Colour: brief white flash, then dark sooty core + bright ragged rim ---
				float radial = saturate(rw / max(1e-3, ballR));
				float flash = exp(-t * 14.0);
				// Soot fills the interior shortly after bloom (real fireballs are not lit solid).
				float sootGrow = smoothstep(0.02, 0.14, t);
				float sootRad = lerp(0.38, 0.68, saturate(t * 0.9));
				float sootMask = SoftDisk(rw, ballR * sootRad, 0.14 + 0.06 * t);
				float sootMottle = 0.4 + 0.6 * Fbm(pw * 3.2 + float2(_Seed, t * 0.8));
				float sootAmt = saturate(sootMask * sootGrow * sootMottle * (1.0 - flash));

				float3 sootCol = lerp(
					_SmokeColor.rgb * 0.35,
					float3(0.18, 0.07, 0.03),
					sootMottle);
				// Charcoal voids in the densest smoke.
				sootCol = lerp(sootCol, _SmokeColor.rgb * 0.15, saturate(1.0 - boil) * 0.55);

				float3 fireCol = lerp(_FireColor.rgb, _FireHotColor.rgb, saturate(tongues * 0.7 + rim));
				// Hot shell / fingers stay bright; mid is orange transition.
				float shellBright = smoothstep(0.32, 0.88, radial) * (0.55 + 0.55 * tongues);
				shellBright = max(shellBright, fingers * 0.9);

				float3 col = fireCol;
				col = lerp(col, sootCol, sootAmt * 0.92);
				col = lerp(col, fireCol * float3(1.15, 1.05, 0.85), shellBright * (1.0 - sootAmt * 0.65));
				// Initial flash (brightest near centre), then gone.
				col = lerp(col, _HotColor.rgb, flash * lerp(0.85, 0.25, radial));
				// Age toward ash/smoke overall.
				col = lerp(col, _SmokeColor.rgb * 1.1, breakup * (0.25 + 0.35 * (1.0 - radial)));
				col *= lerp(0.9, 1.15, boil * shellBright + (1.0 - sootAmt) * 0.3);
				col *= lerp(0.35, 1.0, saturate(lifeEnv + 0.15));

				return float4(col, alpha);
			}
			ENDCG
		}

		// ---- Pass 1: additive electric / spark overlay ----
		Pass
		{
			Blend SrcAlpha One
			Cull Off
			ZWrite Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			v2f vert(appdata v) { return VertCommon(v); }

			float4 frag(v2f i) : SV_Target
			{
				float t = saturate(_Progress);
				float2 pw;
				float rw;
				float diskMask;
				SampleDomain(i.uv, t, pw, rw, diskMask);
				if (diskMask <= 0.001)
				{
					return 0;
				}

				float flashEnv = exp(-t * 22.0) * (1.0 + 0.35 * Hash11(floor(_Time.y * 60.0) + _Seed));
				float lifeEnv = LifeEnvelope(t);
				float bodyEnv = lifeEnv;
				float ringEnv = smoothstep(0.0, 0.025, t) * (1.0 - smoothstep(0.25, 0.55, t));
				float sparkEnv = smoothstep(0.0, 0.04, t) * (1.0 - smoothstep(0.35, 0.85, t));

				float coreR = lerp(0.22, 0.05, saturate(t * 1.8));
				float core = pow(SoftDisk(rw, coreR, 0.12 + 0.08 * t), 1.6);
				float hotSpot = pow(SoftDisk(rw, coreR * 0.45, 0.06), 2.2);

				float downAdd = saturate(-(i.uv.y * 2.0 - 1.0));
				float shellR = RaggedRadius(pw, t, FireballRadius(t), downAdd) * 0.95;
				float shell = SoftDisk(rw, shellR, 0.14 + 0.1 * t);
				float plasmaNoise = 0.55 + 0.45 * Fbm(pw * 5.5 + float2(_Seed, t * 3.0));
				float filaments = abs(FbmPolar(pw, rw * 6.0 - t * 4.0, 2.2, _Seed + 11.0));
				filaments = pow(saturate(1.0 - filaments * 1.8), 3.0);
				float plasma = shell * plasmaNoise * (0.45 + 0.7 * filaments);

				float rings = 0.0;
				float nRings = max(1.0, _RingCount);
				[unroll]
				for (int ri = 0; ri < 3; ri++)
				{
					float idx = (float)ri;
					if (idx >= nRings)
					{
						break;
					}
					float delay = idx * 0.12;
					float tt = saturate((t - delay) / max(1e-3, 1.0 - delay));
					float rad = lerp(0.05, 0.95, pow(tt, 0.72));
					float width = lerp(0.055, 0.018, tt) * (1.0 + 0.25 * idx);
					float ring = pow(ShockRing(rw, rad, width), 1.35);
					float crackle = 0.7 + 0.3 * NoisePolar(pw, rad * 20.0 + _Seed, 8.0, 3.7);
					rings += ring * crackle * exp(-tt * 2.8) * (1.0 - 0.25 * idx);
				}

				float t2 = saturate(t * 1.15 - 0.05);
				float rad2 = lerp(0.02, 0.88, pow(t2, 0.65));
				rings += pow(ShockRing(rw, rad2, lerp(0.03, 0.01, t2)), 2.0) * exp(-t2 * 3.5) * 0.65;

				float sparks = SparkRays(pw, rw, t, _Seed);
				sparks *= (0.55 + 0.45 * SoftDisk(rw, 0.9, 0.2));
				float embers = EmberSparks(pw, t, _Seed);

				// Fire-tinted embers on the additive pass.
				float3 fireEmber = lerp(_FireHotColor.rgb, _SparkColor.rgb, 0.35);

				// Keep additive light on the shell/flash ù do not bleach the dark sooty core.
				float radialAdd = saturate(rw / max(1e-3, shellR));
				float shellGate = lerp(flashEnv, smoothstep(0.28, 0.75, radialAdd), saturate(t * 4.0));

				float3 col = 0;
				col += _HotColor.rgb * (core * 1.8 + hotSpot * 2.6) * flashEnv;
				col += _Color.rgb * plasma * bodyEnv * 0.45 * shellGate;
				col += lerp(_HotColor.rgb, _Color.rgb, 0.35) * rings * ringEnv * 1.7;
				col += _SparkColor.rgb * sparks * sparkEnv * 1.1;
				col += fireEmber * embers * sparkEnv * 1.15;

				float rim = ShockRing(rw, lerp(0.08, 0.9, saturate(t * 1.05)), 0.04) * ringEnv;
				col += float3(0.15, 0.45, 1.0) * rim * 0.35;
				col += _FireHotColor.rgb * rim * 0.25;

				col *= _Intensity * (1.0 + 1.2 * flashEnv) * lifeEnv;

				float alpha =
					saturate(
						(core + hotSpot) * flashEnv * 1.2 +
						plasma * bodyEnv * 0.55 +
						rings * ringEnv * 0.95 +
						sparks * sparkEnv * 0.7 +
						embers * sparkEnv * 0.65
					) * lifeEnv * diskMask;

				col = min(col, 12.0);
				return float4(col, alpha);
			}
			ENDCG
		}
	}

	FallBack Off
}
