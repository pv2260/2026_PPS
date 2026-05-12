// LoomingComet.shader
//
// URP unlit additive shader. Renders a glowing spherical head with a
// trailing tail on an "axial billboard" — the quad's long axis is fixed in
// world space along the tail direction, and the quad rotates around that
// axis to face the camera. From head-on the comet reads as a soft glowing
// sphere; from the side it reads as a streak.
//
// Mesh
//   Apply this material to any unit Quad (Unity built-in Quad works).
//   The shader treats positionOS.y as "along motion" (-0.5 = tail tip,
//   +0.5 = head) and positionOS.x as "side" (-0.5..+0.5). The mesh's
//   actual placement in 3D is overridden by the vertex shader.
//
// Orientation
//   The GameObject's local +Z (Transform.forward) is the motion direction.
//   Point Transform.forward toward the player to make the comet loom
//   toward them.
//
// Tail direction
//   The tail extends along _TailDirection (object space). Default
//   (0, 0, -1) = behind the head, opposite to motion. Set it to any
//   direction for stylized comets:
//     (0, 0, -1) classic trail behind motion
//     (0, -1, 0) drips downward
//     (0,  1, 0) rises like flame
//     (1,  0, 0) wind-swept to the side
//   Magnitude doesn't matter; the shader normalizes.
//
// Motion modes
//   Speed > 0: shader-driven straight-line. The head animates from local
//   Z = 0 to Z = _TravelDistance along Transform.forward. _Loop on with
//   _LoopGapSeconds between runs.
//   Speed = 0: external. Animate transform.position from C# (spline,
//   curve, scripted). The shader renders the head at the transform and
//   the tail along _TailDirection.

Shader "PPS/LoomingComet"
{
    Properties
    {
        [Header(Appearance)]
        [HDR] _Color        ("Color (HDR)",         Color)             = (1.0, 0.85, 0.5, 1.0)
        _Intensity          ("Intensity",           Range(0.0, 20.0)) = 4.0
        _HeadSize           ("Head Size (m)",       Range(0.005, 1.0)) = 0.06
        _CoreSharpness      ("Core Sharpness",      Range(1.0, 16.0)) = 5.0

        [Header(Tail)]
        _TailLength         ("Tail Length (m)",     Range(0.0, 10.0)) = 0.6
        _TailWidth          ("Tail Width Ratio",    Range(0.0, 1.0))  = 0.25
        _TailFalloff        ("Tail Falloff",        Range(0.5, 10.0)) = 3.0
        _TailDirection      ("Tail Direction (Object Space)", Vector) = (0, 0, -1, 0)

        [Header(Motion)]
        _Speed              ("Speed (m/s)",         Float)            = 0.6
        _TravelDistance     ("Travel Distance (m)", Float)            = 2.4
        _StartTime          ("Start Time (s)",      Float)            = 0.0
        [Toggle] _Loop      ("Loop Motion",         Float)            = 1
        _LoopGapSeconds     ("Loop Gap (s)",        Float)            = 0.5
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

            Blend  One One        // additive — looks like emitted light
            ZWrite Off
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
                float  _HeadSize;
                float  _CoreSharpness;

                float  _TailLength;
                float  _TailWidth;
                float  _TailFalloff;
                float4 _TailDirection;

                float  _Speed;
                float  _TravelDistance;
                float  _StartTime;
                float  _Loop;
                float  _LoopGapSeconds;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  along       : TEXCOORD0; // 0 at tail tip, 1 at head
                float  side        : TEXCOORD1; // -0.5..+0.5 across width
                float  visible     : TEXCOORD2; // 0 during loop gap, else 1
            };

            // Returns head's offset along object-space +Z (in meters), plus a
            // visibility flag used to hide the geometry during the loop gap.
            float ComputeHeadZ(out float visible)
            {
                visible = 1.0;

                // Speed <= 0 — shader motion disabled. C# drives the
                // transform. Head sits at the transform's origin.
                if (_Speed <= 1e-5)
                    return 0.0;

                float elapsed  = max(0.0, _Time.y - _StartTime);
                float duration = _TravelDistance / _Speed;

                if (_Loop > 0.5)
                {
                    float period = duration + _LoopGapSeconds;
                    float phase  = fmod(elapsed, period);
                    if (phase > duration)
                    {
                        visible = 0.0; // resting in the gap
                        return 0.0;
                    }
                    return phase * _Speed;
                }

                return clamp(elapsed * _Speed, 0.0, _TravelDistance);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Decode quad-local coordinates from the unit Quad mesh.
                float along = IN.positionOS.y + 0.5; // 0..1
                float side  = IN.positionOS.x;       // -0.5..+0.5

                // Head position in world space — placed along the transform's
                // forward direction by the animated distance, with the
                // transform's origin as the path start.
                float visible;
                float headZ      = ComputeHeadZ(visible);
                float3 originWS  = TransformObjectToWorld(float3(0, 0, 0));
                float3 motionWS  = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
                float3 headPosWS = originWS + motionWS * headZ;

                // Tail extension direction (configurable in inspector).
                // Default (0, 0, -1) = opposite the motion axis = trails behind.
                // The vector is given in object space and transformed to world.
                float3 tailDirOS = _TailDirection.xyz;
                float  tailDirLenSq = dot(tailDirOS, tailDirOS);
                if (tailDirLenSq < 1e-8) tailDirOS = float3(0, 0, -1);
                float3 tailDirWS = normalize(TransformObjectToWorldDir(normalize(tailDirOS)));

                // Axial billboard: "side" axis is perpendicular to the tail
                // direction and also faces the camera. Falls back to world up
                // if the tail direction is parallel to the view direction.
                float3 toCamWS = _WorldSpaceCameraPos.xyz - headPosWS;
                float3 sideWS  = cross(tailDirWS, toCamWS);
                float  sideLen = length(sideWS);
                if (sideLen < 1e-4)
                    sideWS = normalize(cross(tailDirWS, float3(0, 1, 0)));
                else
                    sideWS /= sideLen;

                // Width tapers from the head (full) to the tail tip (_TailWidth).
                float widthFactor = lerp(_TailWidth, 1.0, along);

                // Final world position: start at the head, walk along the tail
                // direction by (1 - along) * tail length, then offset sideways.
                float3 worldPos = headPosWS
                                + tailDirWS * _TailLength * (1.0 - along)
                                + sideWS    * side * (_HeadSize * 2.0) * widthFactor;

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.along       = along;
                OUT.side        = side;
                OUT.visible     = visible;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Hide entire fragment when the loop is in its gap.
                if (IN.visible < 0.5) discard;

                // Radial brightness across the width: 1 at the centerline,
                // 0 at the edge. Raised to a power to make the core feel
                // like a tight glowing sphere rather than a blurry disk.
                float radial     = saturate(abs(IN.side) * 2.0);
                float radialGlow = pow(saturate(1.0 - radial), _CoreSharpness);

                // Tail brightness along the length: 1 at the head, 0 at tip.
                float tailMask   = pow(saturate(IN.along), _TailFalloff);

                float bright     = radialGlow * tailMask * _Intensity;

                half3 rgb = _Color.rgb * bright;
                return half4(rgb, bright); // alpha unused under Blend One One
            }
            ENDHLSL
        }
    }

    FallBack Off
}
