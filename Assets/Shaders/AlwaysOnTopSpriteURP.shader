Shader "0x/AlwaysOnTopSpriteURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)
        [Toggle] _InvertFacing ("Invert Facing (show back instead of front)", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Overlay"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "AlwaysOnTop"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off        // 両面を通し、fragで裏面をdiscard
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float  _InvertFacing;

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS   = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS  = TransformWorldToHClip(posWS);
                o.uv           = TRANSFORM_TEX(v.uv, _MainTex);
                o.color        = v.color * _Color;
                return o;
            }

            // FRONT_FACE_SEMANTIC は frag の引数で受ける
            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // ★ 3引数版マクロで表裏を取得
                bool isFront = IS_FRONT_VFACE(face, true, false);

                // 負スケール（奇数軸のミラー）時は表裏反転
                if (unity_WorldTransformParams.w < 0.0)
                {
                    isFront = !isFront;
                }

                // マテリアルから任意で反転
                if (_InvertFacing > 0.5)
                {
                    isFront = !isFront;
                }

                // 裏面は描画しない（透明）
                if (!isFront) discard;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
