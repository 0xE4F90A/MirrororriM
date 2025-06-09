Shader "Custom/MirrorOrtho"
{
    Properties{
        _Tint ("Tint (�I)", Color) = (1,1,1,1)
    }
    SubShader{
        Tags{ "RenderType"="Opaque"     // ���߃L���[���O�ŕ`��
              "Queue"="Transparent-1" }

        // �@ �����܂łɕ`���ꂽ��ʂ������ŃL���v�`��
        GrabPass{ "_GrabTex" }

        // �A �L���v�`����\�蒼��
        Pass{
            Cull Off ZWrite Off Lighting Off
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GrabTex;           // GrabPass �����X�N���[���摜
            float4   _GrabTex_TexelSize;  // x=1/width, y=1/height
            fixed4   _Tint;               // �F������

            struct appdata{
                float4 vertex : POSITION;
            };
            struct v2f{
                float4 pos    : SV_POSITION;
                float4 grabUV : TEXCOORD0;   // w �œ����␳
            };

            v2f vert (appdata v){
                v2f o;
                o.pos    = UnityObjectToClipPos(v.vertex);
                o.grabUV = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ��ʍ��W (0-1) �֎ˉe
                float2 uv = i.grabUV.xy / i.grabUV.w;

                // ���� �� ���ʂŉ����] �� ����
                uv.x = 1-uv.x;             // ��Plane ���J�������ʂ������������Ȃ炱��� OK
                // �����c�ɔ��]�������Ȃ� uv.y = 1-uv.y;

                fixed4 col = tex2D(_GrabTex, uv) * _Tint;
                return col;
            }
            ENDCG
        }
    }
    // �s�����x���������݂����Ƃ��� Fallback "Unlit/Texture"
}
