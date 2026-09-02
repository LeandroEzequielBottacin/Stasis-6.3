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

        float _StasisGlitchStrength;
        float _StasisGlitchBands;
        float _StasisGlitchRate;
        float _StasisGlitchDensity;
        float _StasisGlitchShift;
        float _StasisGlitchRGBSplit;
        float _StasisGlitchTint;

        TEXTURE2D_X(_StasisMask);
        TEXTURE2D_X(_StasisDilated);
        TEXTURE2D_X(_StasisSceneColor);

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


        float StasisHash1(float n)
        {
            return frac(sin(n * 12.9898) * 43758.5453);
        }

        // Tears the frozen object into horizontal slices and slides them sideways, with a
        // per-channel offset on top. Sampling a copy of the scene colour (rather than the
        // live target) is what lets a slice pull pixels in from somewhere else, which is
        // the part that reads as a glitch rather than as a blur.
        //
        // Returns premultiplied colour; alpha says how much of the original pixel to replace.
        float4 StasisGlitch(float2 uv, float inside, float time, float3 stasisColor)
        {
            if (_StasisGlitchStrength <= 0.0 || inside <= 0.0) return 0;

            // Slices only change on discrete ticks; a continuously moving offset looks
            // like wobble, not like corrupted data.
            float tick = floor(time * max(_StasisGlitchRate, 0.001));
            float band = floor(uv.y * max(_StasisGlitchBands, 1.0));

            float pick = StasisHash1(band * 1.7 + tick * 3.1);
            float active = step(1.0 - saturate(_StasisGlitchDensity), pick);

            float shift = (StasisHash1(band + tick * 7.3) - 0.5) * 2.0 * _StasisGlitchShift * active;

            float2 guv = uv + float2(shift, 0.0);
            float split = _StasisGlitchRGBSplit * (0.35 + 0.65 * active);

            float3 c;
            c.r = SAMPLE_TEXTURE2D_X_LOD(_StasisSceneColor, sampler_LinearClamp, guv + float2(split, 0.0), 0).r;
            c.g = SAMPLE_TEXTURE2D_X_LOD(_StasisSceneColor, sampler_LinearClamp, guv, 0).g;
            c.b = SAMPLE_TEXTURE2D_X_LOD(_StasisSceneColor, sampler_LinearClamp, guv - float2(split, 0.0), 0).b;

            // Blocky bright speckle, so the tear is not just a clean offset.
            float2 blockUv = floor(uv * float2(_StasisGlitchBands * 1.7, _StasisGlitchBands));
            float block = StasisHash1(blockUv.x * 3.7 + blockUv.y * 11.3 + tick * 5.1);
            c += stasisColor * step(0.985, block) * active * 2.0;

            c = lerp(c, c * stasisColor, saturate(_StasisGlitchTint));

            // Only the displaced slices replace the pixel; the rest of the object stays
            // as it was rendered.
            float a = saturate(_StasisGlitchStrength) * inside * max(active, saturate(_StasisGlitchRGBSplit * 40.0));
            return float4(c * a, a);
        }

        float4 FragComposite(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            float2 uv = IN.texcoord;

            float4 dilated = SAMPLE_TEXTURE2D_X_LOD(_StasisDilated, sampler_LinearClamp, uv, 0);
            float field = dilated.a;
            if (field <= 0.0) return 0;   // premultiplied: no colour, no coverage

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

            float3 outline = _StasisColor.rgb * energy;

            // Premultiplied alpha, so one pass can both add the glow (alpha 0) and
            // replace the torn slices (alpha 1) without a second full-screen pass.
            float4 glitch = StasisGlitch(uv, inside, t, _StasisColor.rgb);
            return float4(outline + glitch.rgb, glitch.a);
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
            // Premultiplied alpha: rgb is added, alpha says how much of the existing
            // pixel to remove. Glow uses alpha 0 (pure add), glitch slices use alpha > 0.
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "StasisCopyColor"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            #pragma target 3.5

            // The glitch needs to read scene pixels from somewhere other than where it
            // writes, so the composite samples this copy instead of its own target.
            float4 FragCopy(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, IN.texcoord, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
