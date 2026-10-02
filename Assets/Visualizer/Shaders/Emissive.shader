// Svítící plochy (čočky, LED segmenty tub). HDR hodnoty > 1 dají s Bloomem záři.
Shader "Visualizer/Emissive"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Base ("Off color", Color) = (0.03,0.03,0.035,1)
        _Albedo ("Lit off color (× ambient)", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity;
            float4 _Base;
            float4 _Albedo;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // _Albedo: barva povrchu ve vypnutém stavu osvětlená okolním světlem sálu (mléčný difuzor tub)
                return float4(_Base.rgb + _Albedo.rgb * unity_AmbientSky.rgb + _Color.rgb * _Intensity, 1);
            }
            ENDCG
        }
    }
}
