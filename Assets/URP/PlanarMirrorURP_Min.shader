Shader "Unlit/PlanarMirrorURP_Min"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            // Probe スロット（_PRID_ONE～_FOUR をビルド）
            #pragma multi_compile _PRID_ONE _PRID_TWO _PRID_THREE _PRID_FOUR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // path
            #include "PlanarReflections.cginc"

            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float4 scr : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END

            V vert(A v)
            {
                V o;
                o.posCS = TransformObjectToHClip(v.posOS.xyz);
                o.scr   = ComputeScreenPos(o.posCS);
                return o;
            }

            float4 frag(V i) : SV_Target
            {
                float4 refl = SamplePlanarReflections(i.scr);
                return refl * _Tint;
            }
            ENDHLSL
        }
    }
}
