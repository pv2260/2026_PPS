// BallSplat.shader
//
// 2D billboard paint splat. Draws a flat splatter silhouette on a Quad mesh
// that always faces the camera. The shape is procedural:
//   - main lumpy blob with angular noise so the silhouette is irregular
//   - a few rounded "finger" protrusions where the angular noise crosses
//     a threshold (smoothstep — never pointy)
//   - detached droplets scattered around the main body at fixed angles
//     and randomized distances/sizes per spawn (via _BlobSeed)
//
// Alpha-blended so it reads as opaque paint over the scene rather than the
// glowing additive blob the previous version was. The "look" target is a
// 2D vector splatter graphic, not a 3D goo droplet.
//
// Mesh
//   Use a unit Quad (Unity built-in Quad works). The vertex shader reads
//   positionOS.xy as quad-local coords in [-0.5..+0.5] and ignores the
//   mesh's actual placement in 3D.
//
// C# expectations (set per-instance via MaterialPropertyBlock):
//   _StartTime  — Time.time when the splat spawned
//   _Lifetime   — total animation lifetime (seconds)
//   _PeakSize   — final billboard radius in world units
//   _BlobSeed   — random float, so two splats don't look identical
//   _Color      — pinch tint (or material's authored color)
//
// All five are already pushed by TrajectoryObjectController.InitializeSplatInstance.

Shader "PPS/BallSplat"
{
    Properties
    {
        [HDR] _Color        ("Color (HDR)",        Color)              = (0.20, 0.45, 1.00, 1)
        _PeakSize           ("Peak Size (m)",      Range(0.05, 2.0))   = 0.45
        _Lifetime           ("Lifetime (s)",       Range(0.1, 5.0))    = 1.2
        _StartTime          ("Start Time (s)",     Float)              = 0
        _Intensity          ("Intensity",          Range(0.1, 4.0))    = 1.0
        _BlobSeed           ("Blob Seed",          Float)              = 0

        [Header(Main Blob)]
        _CoreRadius         ("Core Radius",        Range(0.10, 0.45))  = 0.30
        _Irregularity       ("Irregularity",       Range(0, 0.15))     = 0.06

        [Header(Fingers)]
        _FingerCount        ("Finger Count",       Range(2, 14))       = 6
        _FingerStrength     ("Finger Strength",    Range(0, 0.20))     = 0.10
        _FingerRoundness    ("Finger Roundness",   Range(0.05, 0.95))  = 0.55

        [Header(Detached Droplets)]
        _DropletCount       ("Droplet Count",      Range(0, 10))       = 6
        _DropletStrength    ("Droplet Strength",   Range(0, 1))        = 0.8
        _DropletRing        ("Droplet Ring Radius",Range(0.30, 0.48))  = 0.42
        _DropletSize        ("Droplet Size",       Range(0.01, 0.08))  = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend  SrcAlpha OneMinusSrcAlpha   // opaque paint blend, not additive
            ZWrite Off
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _PeakSize;
                float  _Lifetime;
                float  _StartTime;
                float  _Intensity;
                float  _BlobSeed;

                float  _CoreRadius;
                float  _Irregularity;

                float  _FingerCount;
                float  _FingerStrength;
                float  _FingerRoundness;

                float  _DropletCount;
                float  _DropletStrength;
                float  _DropletRing;
                float  _DropletSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0; // 0..1 across the quad
                float  age01       : TEXCOORD1;
            };

            // Hash-based scalar noise from a 2D seed. Output 0..1.
            float Hash2(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float age   = max(0.0, _Time.y - _StartTime);
                float age01 = saturate(age / max(_Lifetime, 1e-4));

                // Quick exponential growth so the splat snaps into place
                // shortly after impact rather than scaling in slowly.
                float grow   = 1.0 - exp(-age01 * 9.0);
                float radius = _PeakSize * grow;

                // Camera-facing billboard. UNITY_MATRIX_V[0]/[1] are world-
                // space right/up vectors of the camera, which means a quad
                // built from them always faces the viewer regardless of how
                // the GameObject's transform is rotated.
                float3 worldOrigin = TransformObjectToWorld(float3(0, 0, 0));
                float3 camRight    = UNITY_MATRIX_V[0].xyz;
                float3 camUp       = UNITY_MATRIX_V[1].xyz;

                float3 worldPos = worldOrigin
                                + camRight * IN.positionOS.x * radius * 2.0
                                + camUp    * IN.positionOS.y * radius * 2.0;

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv          = IN.positionOS.xy + 0.5; // -0.5..+0.5 → 0..1
                OUT.age01       = age01;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (IN.age01 >= 1.0) discard;

                // Center the UV around (0,0). r = distance from center,
                // theta = angle in [-pi, pi]. Used for radial silhouette math.
                float2 p = IN.uv - 0.5;
                float  r = length(p);
                float  theta = atan2(p.y, p.x);

                // --- Main blob silhouette ---
                // Radius varies with angle to give a lumpy / curved outline.
                // Three sine harmonics summed at different frequencies and
                // phases (the phases are driven by _BlobSeed so each splat
                // has a different shape).
                float wobble =
                      sin(theta * 3.0 + _BlobSeed * 1.71)
                    + sin(theta * 5.0 + _BlobSeed * 2.31 + 1.1) * 0.65
                    + sin(theta * 7.0 + _BlobSeed * 3.13 + 2.7) * 0.40;
                wobble /= 2.05; // normalize approx to [-1, +1]

                float silhouette = _CoreRadius + _Irregularity * wobble;

                // --- Rounded finger protrusions ---
                // Sample a higher-frequency angular noise and pass it through
                // smoothstep so the protrusions are wide rounded bulges with
                // curved tips rather than pointy spikes.
                float fingerNoise = sin(theta * _FingerCount + _BlobSeed * 0.7) * 0.5 + 0.5;
                float finger      = smoothstep(1.0 - _FingerRoundness, 1.0, fingerNoise);
                silhouette       += _FingerStrength * finger;

                // Soft edge of the main body — anti-aliased silhouette.
                float bodyAlpha = 1.0 - smoothstep(silhouette - 0.006,
                                                   silhouette + 0.004,
                                                   r);

                // --- Detached droplets ---
                // Place a small fixed pool of droplets around a ring at
                // randomized distances and sizes. Maximum 10 (loop bound is
                // a hard literal so the shader compiler can unroll it).
                float dropletAlpha = 0.0;
                int   dropletN     = (int)floor(_DropletCount + 0.5);
                for (int i = 0; i < 10; i++)
                {
                    if (i >= dropletN) break;

                    float fi = (float)i;
                    float angleJitter = Hash2(float2(fi, _BlobSeed)) * 6.2832;
                    float distJitter  = (Hash2(float2(fi + 11.0, _BlobSeed * 0.31)) - 0.5) * 0.08;
                    float sizeJitter  = (Hash2(float2(fi + 23.0, _BlobSeed * 0.71)) - 0.3) * _DropletSize;

                    float2 dropletCenter = float2(cos(angleJitter), sin(angleJitter))
                                         * (_DropletRing + distJitter);
                    float  dropletR      = max(_DropletSize + sizeJitter, 0.005);

                    float d = length(p - dropletCenter);
                    float a = 1.0 - smoothstep(dropletR - 0.003, dropletR + 0.002, d);
                    dropletAlpha = max(dropletAlpha, a);
                }

                // Combine: main body OR any droplet contributes alpha.
                float alpha = max(bodyAlpha, dropletAlpha * _DropletStrength);

                // Fade out smoothly over the last quarter of the lifetime so
                // the splat disappears rather than popping out.
                float fade = 1.0 - smoothstep(0.70, 1.00, IN.age01);
                alpha *= fade;

                return half4(_Color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
