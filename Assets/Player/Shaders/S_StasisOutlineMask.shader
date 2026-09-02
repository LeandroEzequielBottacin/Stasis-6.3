// Mask for the screen-space stasis outline.
//
// Used as an OVERRIDE MATERIAL by StasisOutlineFeature: the feature draws every renderer
// carrying the stasis rendering layer bit with this shader, so objects need no extra
// material slot of their own. Writes object-space position into RGB and coverage into A;
// the feature grows that into a distance field and paints the electricity in the ring.
Shader "Hidden/Stasis/OutlineMask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StasisMask"

            Cull Back
            ZWrite Off
            // The mask is its own render target with no depth attachment (binding the
            // camera's depth would force this target to match its MSAA sample count),
            // so occlusion is resolved against the depth texture in the fragment instead.
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
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
    }

    Fallback Off
}
