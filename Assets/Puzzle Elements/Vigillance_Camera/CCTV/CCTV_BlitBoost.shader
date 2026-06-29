Shader "Hidden/CCTV_BlitBoost"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _Color("Multiply Color", Color) = (1,1,1,1)
        _Brightness("Brightness", Float) = 1.0
        _Contrast("Contrast", Float) = 1.0
        _Gamma("Gamma", Float) = 1.0
        _Saturation("Saturation", Float) = 1.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
            Cull Off ZTest Always ZWrite Off
            Pass
            {
                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _Color;
                float _Brightness;
                float _Contrast;
                float _Gamma;
                float _Saturation;

                struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
                struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

                v2f vert(appdata v) {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                float3 ApplyContrast(float3 c, float k) { return (c - 0.5) * k + 0.5; }
                float3 ApplyGamma(float3 c, float g) { return pow(max(c, 1e-6), 1.0 / max(g, 1e-6)); }
                float3 ApplySaturation(float3 c, float s) {
                    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                    return lerp(l.xxx, c, s);
                }

                float4 frag(v2f i) :SV_Target
                {
                    float4 col = tex2D(_MainTex, i.uv);
                    col.rgb *= _Brightness;
                    col.rgb = ApplyContrast(col.rgb, _Contrast);
                    col.rgb = ApplyGamma(col.rgb, _Gamma);
                    col.rgb = ApplySaturation(col.rgb, _Saturation);
                    col *= _Color;
                    return col;
                }
                ENDHLSL
            }
        }
}
