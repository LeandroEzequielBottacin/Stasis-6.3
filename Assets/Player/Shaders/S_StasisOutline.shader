// Stasis outline material.
//
// Lives as an EXTRA material slot on the object's Renderer. It drives two different
// outline paths, and _BorderThickness (set by StasisEffect.cs through a
// MaterialPropertyBlock) turns both on and off: 0 hides the outline, ~1 shows it.
//
//  * StasisMask pass  - the screen-space path (default). Marks the object into a mask
//                       that StasisOutlineFeature dilates and composites. Crack-free on
//                       any mesh, because the outline never depends on vertex normals.
//
//  * Hull pass        - the inverted-hull fallback, off by default (_DrawHull). Needs no
//                       renderer feature, but tears open at hard edges where a mesh has
//                       split normals. Enable it only if the feature isn't installed on
//                       the renderer being used.
Shader "Stasis/S_StasisOutline"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.33, 0.78, 0.16, 1)
        _BorderThickness ("Border Thickness (runtime 0..1)", Float) = 1

        [Header(Path)][Space]
        [Toggle] _DrawHull ("Draw Inverted Hull (off = use renderer feature)", Float) = 0

        [Header(Inverted Hull Only)][Space]
        _OutlineWidth ("Outline Width (pixels)", Range(0, 40)) = 6
        _Intensity ("Intensity", Range(0, 20)) = 1.4

        _ArcStrength ("Arc Strength", Range(0, 4)) = 2
        _PlasmaStrength ("Plasma Strength", Range(0, 4)) = 0.45
        _JitterStrength ("Hull Jitter Strength", Range(0, 1)) = 0.4
        _BaseOpacity ("Base Outline Floor", Range(0, 2)) = 0.05

        _ArcScale ("Arc Scale", Range(0.1, 200)) = 55
        _ArcSpeed ("Arc Speed", Range(0, 10)) = 2
        _ArcThinness ("Arc Thinness", Range(1, 200)) = 70
        _ArcSharpness ("Arc Falloff", Range(0.5, 8)) = 1.5
        _ArcBranching ("Arc Branching", Range(0, 1)) = 0.7

        _PlasmaScale ("Plasma Scale", Range(0.1, 40)) = 8
        _PlasmaSpeed ("Plasma Speed", Range(0, 10)) = 0.8

        _JitterScale ("Jitter Scale", Range(0.1, 40)) = 9
        _JitterSpeed ("Jitter Speed", Range(0, 10)) = 3

        _FresnelPower ("Fresnel Power", Range(0, 8)) = 1.5
        _FresnelBlend ("Fresnel Blend", Range(0, 1)) = 0.35
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.35
        _FlickerSpeed ("Flicker Speed", Range(0, 60)) = 18

        // An inverted hull only shows as an outline because the object's own depth
        // carves away the part of the shell hidden behind it. A transparent base
        // material writes no depth, and the shell then floods the whole silhouette.
        [Toggle] _DepthPrime ("Depth Prepass (needed on transparent base materials)", Float) = 1

        [Toggle(_SMOOTH_NORMALS)] _SmoothNormals ("Use Baked Smooth Normals (UV3)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            // The hull works in object space; batching would rewrite it into world space.
            "DisableBatching" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "StasisElectric.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _BorderThickness;
            float _DrawHull;
            float _OutlineWidth;
            float _Intensity;

            float _ArcStrength;
            float _PlasmaStrength;
            float _JitterStrength;
            float _BaseOpacity;

            float _ArcScale;
            float _ArcSpeed;
            float _ArcThinness;
            float _ArcSharpness;
            float _ArcBranching;

            float _PlasmaScale;
            float _PlasmaSpeed;

            float _JitterScale;
            float _JitterSpeed;

            float _FresnelPower;
            float _FresnelBlend;
            float _FlickerStrength;
            float _FlickerSpeed;

            float _DepthPrime;
            float _SmoothNormals;
        CBUFFER_END

        // A clip position that every triangle is trivially rejected on. Used instead of
        // a zero-thickness hull, which would z-fight with the surface it sits on.
        #define STASIS_DEGENERATE_CS float4(2, 2, 2, 1)

        bool StasisIsActive() { return _BorderThickness > 1e-4; }

        float StasisHullWidth() { return _OutlineWidth * _BorderThickness; }
        ENDHLSL

        // ------------------------------------------------------------------- mask
        // Writes object-space position into RGB and coverage into A. The renderer
        // feature dilates this and reads the position back, so the electricity stays
        // anchored to the object rather than swimming across the screen.
        Pass
        {
            Name "StasisMask"
            Tags { "LightMode" = "StasisMask" }

            Cull Back
            ZWrite Off
            // The mask is its own render target with no depth attachment (binding the
            // camera's depth would force this target to match its MSAA sample count),
            // so occlusion is resolved against the depth texture in the fragment instead.
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex vertMask
            #pragma fragment fragMask
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            MaskVaryings vertMask(MaskAttributes IN)
            {
                MaskVaryings OUT;
                if (!StasisIsActive())
                {
                    OUT.positionCS = STASIS_DEGENERATE_CS;
                    OUT.positionOS = 0;
                    return OUT;
                }

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 fragMask(MaskVaryings IN) : SV_Target
            {
                // Don't mark parts of the object that are hidden behind other geometry,
                // or the outline would show straight through the wall in front of it.
                // Compared in eye space so it behaves the same on reversed-Z platforms.
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEye = LinearEyeDepth(IN.positionCS.z, _ZBufferParams);
                // A transparent base material writes no depth, so sceneEye lands on
                // whatever is behind the object and it still marks correctly.
                clip(sceneEye - (fragEye - max(0.02, fragEye * 0.005)));

                return half4(IN.positionOS, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------- hull prepass
        Pass
        {
            Name "StasisOutlineDepthPrime"
            // URP's DrawObjects pass draws SRPDefaultUnlit before UniversalForward, so
            // this always lands in the depth buffer before the hull pass tests against it.
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertPrime
            #pragma fragment fragPrime
            #pragma target 3.0

            float4 vertPrime(float4 positionOS : POSITION) : SV_POSITION
            {
                // Nothing to carve when the hull is off: leave the depth buffer alone so
                // toggling stasis can't change how anything else sorts.
                if (_DrawHull < 0.5 || _DepthPrime < 0.5 || StasisHullWidth() <= 1e-4)
                    return STASIS_DEGENERATE_CS;

                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 fragPrime() : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------- hull
        Pass
        {
            Name "StasisOutlineHull"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front       // <- the inverted hull: we only draw the far shell
            ZWrite Off
            ZTest LEqual     // <- the depth laid down above carves the silhouette
            Blend One One    // additive, so it glows into bloom

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local_vertex _SMOOTH_NORMALS

            struct Attributes
            {
                float4 positionOS     : POSITION;
                float3 normalOS       : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float width = StasisHullWidth();
                if (_DrawHull < 0.5 || width <= 1e-4)
                {
                    OUT.positionCS = STASIS_DEGENERATE_CS;
                    OUT.positionOS = 0;
                    OUT.normalWS = float3(0, 1, 0);
                    OUT.positionWS = 0;
                    return OUT;
                }

                float3 posOS = IN.positionOS.xyz;
                float3 shadeNrmOS = normalize(IN.normalOS);

                // The direction the hull is pushed along. On hard-edged meshes the face
                // normals disagree at a corner and the hull tears open there; the baked
                // averaged normal in UV3 agrees across the seam and closes it.
                float3 extrudeNrmOS = shadeNrmOS;
            #if defined(_SMOOTH_NORMALS)
                // Guard against a mesh that has no UV3: normalize(0) is a NaN.
                float smoothLen = length(IN.smoothNormalOS);
                extrudeNrmOS = (smoothLen > 1e-4) ? IN.smoothNormalOS / smoothLen : shadeNrmOS;
            #endif

                // Modulating the screen-space width, rather than nudging the vertex a few
                // object-space millimetres, is what actually reads: the band visibly swells
                // and pinches along its length, and it behaves the same whether the object
                // is a 0.5m crate or a 20m train car.
                if (_JitterStrength > 0.0)
                {
                    float3 jp = posOS * _JitterScale + float3(0.0, -_Time.y * _JitterSpeed, _Time.y * _JitterSpeed * 0.6);
                    float j = saturate((StasisFbm(jp) - 0.34) * 3.2);
                    width *= lerp(1.0, j * 1.8, _JitterStrength);
                }

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(shadeNrmOS));
                float3 extrudeNrmWS = normalize(TransformObjectToWorldNormal(extrudeNrmOS));

                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, extrudeNrmWS);

                // Normalising in pixel space (rather than NDC) keeps the width equal
                // horizontally and vertically on non-square viewports.
                float2 dirPix = normalCS.xy * _ScreenParams.xy;
                float len = length(dirPix);
                // A normal pointing straight at or away from the camera has no screen
                // direction; fall back to a fixed axis so it never becomes a NaN.
                dirPix = (len > 1e-5) ? dirPix / len : float2(0, 1);

                float w = max(positionCS.w, 1e-4);
                positionCS.xy += dirPix * width * 2.0 / _ScreenParams.xy * w;

                OUT.positionCS = positionCS;
                OUT.positionOS = posOS;
                OUT.normalWS = normalWS;
                OUT.positionWS = positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // abs() because we are shading backfaces: the normal points away from us.
                float ndv = abs(dot(N, V));
                float fresnel = pow(saturate(1.0 - ndv), _FresnelPower);
                // Blend rather than multiply, so the outline never fully disappears on
                // the parts of the hull that face the camera head-on.
                float rim = lerp(1.0, fresnel, _FresnelBlend);

                float t = _Time.y;

                float arc = _ArcStrength > 0.0
                    ? StasisArcs(IN.positionOS, t, _ArcScale, _ArcSpeed, _ArcThinness, _ArcSharpness, _ArcBranching)
                    : 0.0;

                float plasma = _PlasmaStrength > 0.0
                    ? StasisPlasma(IN.positionOS, t, _PlasmaScale, _PlasmaSpeed)
                    : 0.0;

                float energy = _BaseOpacity + arc * _ArcStrength + plasma * _PlasmaStrength;
                energy *= rim * StasisFlicker(t, _FlickerStrength, _FlickerSpeed) * _Intensity;

                // Alpha is unused by the additive blend; _Color.a is deliberately ignored
                // so an alpha of 0 left on the material can't blank the outline out.
                return half4(_Color.rgb * energy, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
