Shader "URP/LineLaser"
{
    Properties
    {
        _BaseColor ("Base (HDR) Color", Color) = (1, 0.15, 0.03, 1)     // color del láser (usa HDR para bloom)
        _CoreIntensity ("Core Intensity", Range(0,10)) = 4.0            // multiplica el centro (brillo)
        _EdgeSharpness ("Edge Sharpness", Range(0.1,16)) = 6.0          // qué tan rápido cae hacia los bordes
        _BaseMap ("Stripe/Gradient (R8)", 2D) = "white" {}              // textura 1D/gradiente para el haz
        _Tiling ("UV Tiling X", Float) = 1.0                             // tiling en X (a lo largo)
        _ScrollSpeed ("Scroll Speed", Float) = 2.0                       // velocidad de scroll X
        _NoiseMap ("Noise (R)", 2D) = "gray" {}                          // noise opcional para trémolo
        _NoiseAmp ("Noise Amplitude", Range(0,1)) = 0.2                  // cuánto distorsiona
        _NoiseSpeed ("Noise Speed", Float) = 1.5                         // velocidad del noise
        _DepthFade ("Depth Fade", Range(0,2)) = 0.4                      // mezcla con la profundidad de la escena
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1.0                  // opacidad global (antes de blending)
    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline"}
        LOD 100

        // Blending aditivo
        Blend One One
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Requisitos URP
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);     SAMPLER(sampler_NoiseMap);
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _CoreIntensity;
                float _EdgeSharpness;
                float _Tiling;
                float _ScrollSpeed;
                float _NoiseAmp;
                float _NoiseSpeed;
                float _DepthFade;
                float _GlobalAlpha;
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv0          : TEXCOORD0;  // LineRenderer: x a lo largo, y transversal (0..1)
                float4 color        : COLOR;      // gradiente del LineRenderer
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float4 screenPos  : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv0, _BaseMap);
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            // Depth sampling (como SoftParticles)
            float SampleSceneDepth(float4 screenPos)
            {
                float2 uv = screenPos.xy / screenPos.w;
                #if UNITY_REVERSED_Z
                    float raw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_LinearClamp, uv).r;
                    return raw;
                #else
                    float raw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_LinearClamp, uv).r;
                    return raw; // URP maneja conversión con LinearEyeDepth si la necesitás
                #endif
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // --- UVs ---
                // a lo largo (x) + scroll + pequeño warp por noise
                float t = _Time.y;
                float2 noiseUV = IN.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + float2(0, t * _NoiseSpeed);
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r * 2.0 - 1.0;

                float uAlong = IN.uv.x * _Tiling + t * _ScrollSpeed + noise * _NoiseAmp;
                float2 baseUV = float2(uAlong, 0.5); // textura 1D: usamos fila central

                float stripe = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV).r;

                // --- Perfil transversal: núcleo brillante y caída hacia bordes ---
                // uv.y ~ 0..1 (0 y 1 son bordes). Creamos distancia al centro:
                float edge = abs(IN.uv.y - 0.5) * 2.0;      // 0 centro, 1 borde
                float falloff = saturate(pow(1.0 - edge, _EdgeSharpness));

                // Intensificamos el núcleo y multiplicamos por el gradiente del LineRenderer
                float brightness = (falloff * (1.0 + _CoreIntensity * pow(1.0 - edge, 8.0))) * stripe;

                // --- Depth fade (contra la escena) ---
                // Requiere "Require Depth Texture" activado en la URP Asset.
                float sceneRaw = SampleSceneDepth(IN.screenPos);
                // Convertimos a espacio lineal (ojo: en URP, LinearEyeDepth depende de proyección; simplificamos con diferencia en clip)
                // Un depth fade simple basado en diferencia de clip:
                float fragDepth = IN.screenPos.z / IN.screenPos.w;
                float sceneDepth = sceneRaw; // ya está en el mismo espacio que fragDepth en URP RT
                float d = saturate((sceneDepth - fragDepth) / max(_DepthFade, 1e-4));
                // Si la cámara no expone depth, 'd' ~ 1 y no molesta.
                
                // --- Color final ---
                float4 col = _BaseColor * IN.color;  // combina HDR base + gradiente del LineRenderer
                col.rgb *= brightness * d;
                col.a = _GlobalAlpha;                // alpha solo por cortesía; el blend es One One
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
