Shader "CCTV/UnlitAtlasURP"
{
    Properties
    {
        _BaseMap("Atlas", 2D) = "black" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _EmissionColor("Emission Color", Color) = (1,1,1,0)
        _EmissionStrength("Emission Strength", Float) = 0.0
    }
        SubShader
        {
            Tags{ "RenderPipeline" = "UniversalRenderPipeline" "RenderType" = "Opaque" "Queue" = "Geometry"}
            LOD 100
            Cull Back ZWrite On ZTest LEqual

            Pass
            {
                Name "Unlit"
                Tags{ "LightMode" = "UniversalForward" }
                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

                TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
                float4 _BaseColor;
                float4 _EmissionColor;
                float _EmissionStrength;

                struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
                struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

                Varyings vert(Attributes v) {
                    Varyings o;
                    o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                    o.uv = v.uv;
                    return o;
                }

                half4 frag(Varyings i) :SV_Target
                {
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                    half3 baseCol = tex.rgb * _BaseColor.rgb;
                    baseCol += _EmissionColor.rgb * _EmissionStrength;
                    return half4(baseCol, 1);
                }
                ENDHLSL
            }
        }
}
