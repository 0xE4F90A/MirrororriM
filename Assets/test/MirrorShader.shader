Shader "Unlit/MirrorShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}	// ?Κε?
        _MaskTex ("MaskTex", 2D) = "white" {}	// Υγ«?
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.uv.x = 1 - i.uv.x; // uvΆE½ό
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 maskcol = tex2D(_MaskTex, i.uv);
                clip(col.r - maskcol.r); // pΥγ«Ω
                return col;
            }
            ENDCG
        }
    }
}
