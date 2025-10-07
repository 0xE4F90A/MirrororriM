Shader "Custom/PlanarMirror_SDF"
{
    Properties
    {
        _Tint       ("Tint", Color) = (1,1,1,1)
        _Strength   ("Strength", Range(0,1)) = 1
        _BlockTex   ("Block Texture", 2D) = "white" {}
        _MaxSteps   ("Max Steps", Range(16,256)) = 96
        _MaxDist    ("Max Distance", Range(1,200)) = 50
        _Eps        ("Hit Epsilon", Range(0.00001, 0.001)) = 0.001
        _StepMul    ("Step Multiplier", Range(0.5,2)) = 1
        _EdgeFade   ("Edge Fade (screen)", Range(0,0.2)) = 0.02
    }
    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags{ "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest  LEqual
            Cull   Back
            Blend  One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Strength;
                float  _MaxSteps;
                float  _MaxDist;
                float  _Eps;
                float  _StepMul;
                float  _EdgeFade;
            CBUFFER_END

            TEXTURE2D(_BlockTex); SAMPLER(sampler_BlockTex);

            // ====== キューブ群（SDF）を外部から受ける ======
            struct BoxData { float3 center; float3 half; }; // ワールド座標 / 半寸
            StructuredBuffer<BoxData> _Boxes;
            int _BoxCount;

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS  = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS    = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            // 画面端フェード
            float EdgeFade(float4 hcs, float edge)
            {
                float2 uv = GetNormalizedScreenSpaceUV(hcs);
                float m = min(min(uv.x,uv.y), min(1-uv.x,1-uv.y));
                return saturate(m / max(edge,1e-5));
            }

            // AABB キューブのSDF（回転なし）
            float sdBox(float3 p, float3 b)
            {
                float3 q = abs(p) - b;
                return length(max(q,0)) + min(max(q.x,max(q.y,q.z)), 0);
            }

            // シーンSDF（全キューブの min）
            float SceneSDF(float3 p, out int hitIndex)
            {
                float d = 1e9; hitIndex = -1;
                [loop]
                for (int i=0;i<_BoxCount;i++)
                {
                    float di = sdBox(p - _Boxes[i].center, _Boxes[i].half);
                    if (di < d) { d = di; hitIndex = i; }
                }
                return d;
            }

            float3 EstimateNormal(float3 p)
            {
                const float e = 1e-3;
                int h;
                float dx = SceneSDF(p + float3(e,0,0), h) - SceneSDF(p - float3(e,0,0), h);
                float dy = SceneSDF(p + float3(0,e,0), h) - SceneSDF(p - float3(0,e,0), h);
                float dz = SceneSDF(p + float3(0,0,e), h) - SceneSDF(p - float3(0,0,e), h);
                return normalize(float3(dx,dy,dz));
            }

            // 反射面上の点から反射レイを出して SDF をマーチ
            half4 Frag(Varyings i) : SV_Target
            {
                float3 P = i.positionWS;
                float3 N = normalize(i.normalWS);

                // カメラの視線
                float3 camFwd = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(0,0,-1)));
                float  isOrtho = unity_OrthoParams.w; // 正射=1
                float3 persp   = normalize(P - _WorldSpaceCameraPos);
                float3 viewRay = normalize(lerp(persp, camFwd, isOrtho));

                // 反射方向
                float3 I = -viewRay;
                float3 R = reflect(I, N);

                // マーチ
                float t = 1e-3;
                int stepMax = (int)_MaxSteps;
                int hitIdx = -1;
                float d = 0;

                [loop]
                for (int s=0;s<stepMax;s++)
                {
                    float3 q = P + R * t;
                    d = SceneSDF(q, hitIdx);
                    if (d < _Eps) break;
                    t += d * _StepMul;
                    if (t > _MaxDist) break;
                }

                float4 outCol = float4(0,0,0,1);

                if (d < _Eps && hitIdx >= 0)
                {
                    float3 q = P + R * t;
                    float3 n = EstimateNormal(q);

                    // 簡易キューブUV（支配的な軸で投影）
                    float3 an = abs(n);
                    float2 uv;
                    if (an.x > an.y && an.x > an.z) uv = q.zy;     // X面 → ZY
                    else if (an.y > an.z)           uv = q.xz;     // Y面 → XZ
                    else                            uv = q.xy;     // Z面 → XY

                    uv *= 0.25; // スケール調整（必要ならプロパティ化）
                    uv = frac(uv);

                    float3 albedo = SAMPLE_TEXTURE2D(_BlockTex,sampler_BlockTex,uv).rgb;

                    // フェイクライティング（環境+半Lambert）
                    float3 L = normalize(float3(0.3,0.8,0.4));
                    float ndl = saturate(dot(n,L))*0.6 + 0.4;

                    outCol.rgb = albedo * ndl;
                }
                else
                {
                    // ヒット無し：薄く鏡色
                    outCol.rgb = _Tint.rgb * 0.1;
                }

                // 反射強度とエッジフェード
                float w = _Strength * EdgeFade(i.positionHCS, _EdgeFade);
                return float4(outCol.rgb * _Tint.rgb, 1) * w;
            }
            ENDHLSL
        }
    }
}
