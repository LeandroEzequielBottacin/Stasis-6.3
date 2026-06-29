// TileBrightness.shader
// Simple blit shader with per-tile brightness multiplier.
// Create a Material with this shader and assign it to 'tileBlitMaterial' in CCTVPlaneAtlas.

Shader "Hidden/CCTV/TileBrightness"
{
    Properties{
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Brightness("Brightness", Float) = 1.0
    }
        SubShader{
            Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
            Pass {
                ZTest Always Cull Off ZWrite Off
                Blend One Zero

                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _Color;
                float  _Brightness;

                struct appdata {
                    float4 vertex : POSITION;
                    float2 uv     : TEXCOORD0;
                };
                struct v2f {
                    float4 vertex : SV_POSITION;
                    float2 uv     : TEXCOORD0;
                };

                v2f vert(appdata v) {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target {
                    fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                    c.rgb *= _Brightness;
                    return c;
                }
                ENDHLSL
            }
        }
}
