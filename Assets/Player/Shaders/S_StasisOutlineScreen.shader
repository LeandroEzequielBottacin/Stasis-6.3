// Screen-space half of the stasis outline. Driven by StasisOutlineFeature.cs.
//
// Objects in stasis draw their object-space position + coverage into a mask (the
// StasisMask pass over in S_StasisOutline.shader). Here we grow that mask outwards into
// a distance field, then paint the electricity in the ring between the grown edge and
// the original silhouette.
//
// Growing a mask cannot tear the way an extruded hull does, so this is crack-free on
// hard-edged meshes. Carrying the object-space position through the dilation is what
// keeps the arcs anchored to the object instead of swimming across the screen.
Shader "Hidden/Stasis/OutlineScreen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "StasisElectric.hlsl"

        float4 _StasisTexelSize;   // (1/w, 1/h, w, h) of the mask
        float _StasisRadius;       // outline width, in mask pixels
        int _StasisTaps;           // samples per side, per dilation pass

        float4 _StasisColor;
        float _StasisIntensity;
        float _StasisBaseFloor;
        float _StasisEdgeFalloff;
        float _StasisInnerGlow;

        float _StasisArcStrength;
        float _StasisArcScale;
        float _StasisArcSpeed;
        float _StasisArcThinness;
        float _StasisArcSharpness;
        float _StasisArcBranching;

        float _StasisPlasmaStrength;
        float _StasisPlasmaScale;
        float _StasisPlasmaSpeed;

        float _StasisJitterStrength;
        float _StasisJitterScale;
        float _StasisJitterSpeed;

        float _StasisFlickerStrength;
        float _StasisFlickerSpeed;

        TEXTURE2D_X(_StasisMask);
        TEXTURE2D_X(_StasisDilated);

        // Grow the mask outwards into a distance field, in one pass over a disc.
        //
        // Each tap is scored as (coverage - distance/radius), so the max builds a linear
        // falloff: 1 on the object, fading to 0 _StasisRadius pixels out. The winning
        // tap's object-space position is carried along, which is what anchors the arcs.
        //
        // This deliberately isn't the usual separable two-pass dilation. Separable max
        // makes a whole row (then column) inherit its position from one source pixel, so
        // the carried position smears into hard axis-aligned streaks and the noise on top
        // of it streaks with it. Sampling a disc keeps neighbouring outline pixels mapped
        // to neighbouring surface points, and gives a true Euclidean falloff for free.
        float4 FragDilate(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            float2 uv = IN.texcoord;

            float4 best = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

            int taps = max(_StasisTaps, 1);
            float invRadius = 1.0 / max(_StasisRadius, 1e-3);

            [loop] for (int i = 0; i < taps; i++)
            {
                // Vogel disc: sqrt spacing keeps the samples evenly spread by area, the
                // golden angle keeps successive rings from lining up.
                float r = sqrt((i + 0.5) / taps) * _StasisRadius;
                float theta = i * 2.39996323;

                float2 offset = float2(cos(theta), sin(theta)) * r * _StasisTexelSize.xy;
                float4 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0);

                float v = s.a - r * invRadius;
                if (v > best.a) best = float4(s.rgb, v);
            }

            return float4(best.rgb, saturate(best.a));
        }

        float4 FragComposite(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            float2 uv = IN.texcoord;

            float4 dilated = SAMPLE_TEXTURE2D_X_LOD(_StasisDilated, sampler_LinearClamp, uv, 0);
            float field = dilated.a;
            if (field <= 0.0) return 0;

            // Point-sample the original silhouette so the inner edge stays crisp.
            float inside = SAMPLE_TEXTURE2D_X_LOD(_StasisMask, sampler_PointClamp, uv, 0).a;

            float3 posOS = dilated.rgb;
            float t = _Time.y;

            // Layer 3: swell and pinch the band along its length. Raising the floor the
            // field has to clear locally is what makes the ring look unstable.
            float width = 1.0;
            if (_StasisJitterStrength > 0.0)
            {
                float3 jp = posOS * _StasisJitterScale
                          + float3(0.0, -t * _StasisJitterSpeed, t * _StasisJitterSpeed * 0.6);
                float j = saturate((StasisFbm(jp) - 0.34) * 3.2);
                width = lerp(1.0, j * 1.8, _StasisJitterStrength);
            }

            float band = saturate(1.0 - (1.0 - field) / max(width, 0.02));

            // Keep the ring outside the silhouette, optionally letting some energy bleed
            // back over the object itself.
            band *= lerp(1.0 - inside, 1.0, saturate(_StasisInnerGlow));
            if (band <= 0.0) return 0;

            band = pow(saturate(band), max(_StasisEdgeFalloff, 0.01));

            float arc = _StasisArcStrength > 0.0
                ? StasisArcs(posOS, t, _StasisArcScale, _StasisArcSpeed,
                             _StasisArcThinness, _StasisArcSharpness, _StasisArcBranching)
                : 0.0;

            float plasma = _StasisPlasmaStrength > 0.0
                ? StasisPlasma(posOS, t, _StasisPlasmaScale, _StasisPlasmaSpeed)
                : 0.0;

            float energy = _StasisBaseFloor
                         + arc * _StasisArcStrength
                         + plasma * _StasisPlasmaStrength;

            energy *= band
                    * StasisFlicker(t, _StasisFlickerStrength, _StasisFlickerSpeed)
                    * _StasisIntensity;

            return float4(_StasisColor.rgb * energy, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "StasisDilate"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "StasisComposite"
            Blend One One   // additive, so the glow feeds bloom
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.5
            ENDHLSL
        }
    }

    Fallback Off
}
