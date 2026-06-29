/*
Trail ������Ʈ�� Tiling ���� �����Ǿ����. (�׷��� ��ũ���� ���� UV�� Trail ���׸�Ʈ ũ��� ��� ���� �����ϰ� ����)
*/

Shader "MoveToTrailUV/MoveToTrailUV_Add"
{
    Properties
    {
        _MainTex("Main Texture (RGB)", 2D) = "white" {}
        _MainTexVFade("MainTex V Fade", Range(0, 1)) = 0
        _MainTexVFadePow("MainTex V Fade Pow", Float) = 1
        _MainTexPow("Main Texture Gamma", Float) = 1
        _MainTexMultiplier("Main Texture Multiplier", Float) = 1
        _TintTex("Tint Texture (RGB)", 2D) = "white" {}
        _Multiplier("Multiplier", Float) = 1
        _MainScrollSpeedU("Main Scroll U Speed", Float) = 10
        _MainScrollSpeedV("Main Scroll V Speed", Float) = 0
        _ViewFade("View Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"
        }
        Blend One One // Additive
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float2 uvOrigin : TEXCOORD1; // ���� UV
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _TintTex;

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half _MainTexVFade;
                half _MainTexVFadePow;
                half _MainTexPow;
                half _MainTexMultiplier;
                half _Multiplier;
                half _MainScrollSpeedU;
                half _MainScrollSpeedV;
                half _ViewFade;
                half _MoveToMaterialUV;
            
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                o.uv.x -= frac(_Time.x * _MainScrollSpeedU) + _MoveToMaterialUV;
                o.uv.y -= frac(_Time.x * _MainScrollSpeedV);
                o.uvOrigin = IN.uv;
                o.color = IN.color;
                
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 main = tex2D(_MainTex, IN.uv).rgb;

                half vMask = 1 - abs(IN.uvOrigin.y * 2 - 1);
                vMask = pow(saturate(vMask), _MainTexVFadePow);
                vMask = lerp(1, vMask, _MainTexVFade);

                half lum = (main.r + main.g + main.b) * (1.0h / 3.0h);
                lum = pow(saturate(lum), _MainTexPow) * _MainTexMultiplier;

                half4 ramp = tex2D(_TintTex, half2(lum, 0.5h));

                half opacity = saturate(IN.color.a * _Multiplier) * vMask;

                half3 rgb = ramp.rgb * IN.color.rgb * opacity;

                return half4(rgb * _ViewFade, opacity);
            }
            ENDHLSL
        }
    }
}