// Falešný volumetrický paprsek: aditivní kužel, měkké okraje, slábne s délkou,
// ořízne se o podlahu. Síla hazu je globální (_Haze), nastavuje ji SceneBuilder.
// Připravené na VR (single-pass instanced stereo).
Shader "Visualizer/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _EdgeSoftness ("Edge softness", Range(0.5, 6)) = 2
        _LengthFalloff ("Length falloff", Range(0.5, 4)) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity;
            float _EdgeSoftness;
            float _LengthFalloff;
            float _Haze;
            float _FloorY;
            float _CeilingY;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float t : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.t = v.uv.y;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                // Okraje kužele (silueta) jsou průhledné, střed hustší
                float edge = pow(abs(dot(normalize(i.wn), V)), _EdgeSoftness);
                // Paprsek slábne se vzdáleností od světla
                float along = pow(saturate(1.0 - i.t), _LengthFalloff);
                // Pod podlahou a nad stropem nic
                float floorFade = saturate((i.wpos.y - _FloorY) / 0.05);
                float ceilFade = saturate((_CeilingY - i.wpos.y) / 0.05);
                float a = edge * along * floorFade * ceilFade * _Intensity * _Haze;
                // alfa 0: aditivní světlo nezakrývá passthrough (kompozitor bere alfu jako krytí)
                return float4(_Color.rgb * a, 0);
            }
            ENDCG
        }
    }
}
