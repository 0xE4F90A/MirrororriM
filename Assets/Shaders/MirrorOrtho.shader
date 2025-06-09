// Assets/Shaders/MirrorOrtho.shader
Shader "Unlit/MirrorOrtho"
{
    Properties { _MainTex ("MirrorRT", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off     // — –Ê‚à‰f‚é‚æ‚¤‚É
            ZWrite Off
            Lighting Off
            SetTexture [_MainTex] { combine texture }   // RT ‚ð‚»‚Ì‚Ü‚Ü•\Ž¦
        }
    }
}
