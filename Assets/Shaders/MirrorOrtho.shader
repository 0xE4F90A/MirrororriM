// Assets/Shaders/MirrorOrtho.shader
Shader "Unlit/MirrorOrtho"
{
    Properties { _MainTex ("MirrorRT", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off     // ���ʂ��f��悤��
            ZWrite Off
            Lighting Off
            SetTexture [_MainTex] { combine texture }   // RT �����̂܂ܕ\��
        }
    }
}
