Shader "Custom/MirrorOrtho"
{
    Properties{
        _Tint ("Tint (可選)", Color) = (1,1,1,1)
    }
    SubShader{
        Tags{ "RenderType"="Opaque"     // 透過キュー直前で描く
              "Queue"="Transparent-1" }

        // ① ここまでに描かれた画面を自動でキャプチャ
        GrabPass{ "_GrabTex" }

        // ② キャプチャを貼り直す
        Pass{
            Cull Off ZWrite Off Lighting Off
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GrabTex;           // GrabPass が作るスクリーン画像
            float4   _GrabTex_TexelSize;  // x=1/width, y=1/height
            fixed4   _Tint;               // 色味調整

            struct appdata{
                float4 vertex : POSITION;
            };
            struct v2f{
                float4 pos    : SV_POSITION;
                float4 grabUV : TEXCOORD0;   // w で透視補正
            };

            v2f vert (appdata v){
                v2f o;
                o.pos    = UnityObjectToClipPos(v.vertex);
                o.grabUV = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 画面座標 (0-1) へ射影
                float2 uv = i.grabUV.xy / i.grabUV.w;

                // ── ★ 鏡面で横反転 ★ ──
                uv.x = 1-uv.x;             // ←Plane がカメラ正面を向く直立鏡ならこれで OK
                // もし縦に反転したいなら uv.y = 1-uv.y;

                fixed4 col = tex2D(_GrabTex, uv) * _Tint;
                return col;
            }
            ENDCG
        }
    }
    // 不透明度を書き込みたいときは Fallback "Unlit/Texture"
}
