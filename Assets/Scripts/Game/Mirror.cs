/**************************************************************
 *  Mirror.cs
 *  3D �I�u�W�F�N�g�ɃA�^�b�`���邾���ŋ����iBuilt-in / URP / HDRP�j
 *  Author : 0xE4F90A �����ŏ��R�A
 *************************************************************/

using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class Mirror : MonoBehaviour
{
    [Header("�����`�悷��𑜓x (�����`)")]
    [SerializeField] int textureSize = 1024;

    [Header("���[�J���@�����g�� (�ʏ�� forward)")]
    [SerializeField] Vector3 localNormal = Vector3.forward;

    Camera mirrorCam;          // �����������˃J����
    RenderTexture rt;                // ���p RT
    Renderer rend;               // �Ώ� Renderer
    List<Material> runtimeMats;      // �����������}�e���A��

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"); // URP/HDRP

    /*========================= ������ =========================*/

    void OnEnable()
    {
        rend = GetComponent<Renderer>();

        // �@ RenderTexture �쐬
        rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = $"MirrorRT_{name}",
            antiAliasing = 1,
            hideFlags = HideFlags.DontSave,
            useMipMap = false
        };

        // �A �}�e���A������ �� RT �������ւ�
        runtimeMats = new List<Material>(rend.sharedMaterials.Length);
        for (int i = 0; i < rend.sharedMaterials.Length; ++i)
        {
            Material inst = Instantiate(rend.sharedMaterials[i]);
            inst.name = rend.sharedMaterials[i].name + " (MirrorInst)";
            ReplaceTextureSlot(inst, rt);
            runtimeMats.Add(inst);
        }
        rend.materials = runtimeMats.ToArray();      // �C���X�^���X�z���K�p

        // �B ��\���J��������
        GameObject camObj = new GameObject($"MirrorCam_{name}");
        camObj.hideFlags = HideFlags.HideAndDontSave;
        mirrorCam = camObj.AddComponent<Camera>();
        mirrorCam.enabled = false;
        mirrorCam.targetTexture = rt;
    }

    /*========================= ��n�� =========================*/

    void OnDisable()
    {
        if (mirrorCam) DestroyImmediate(mirrorCam.gameObject);
        if (rt) DestroyImmediate(rt);
        if (runtimeMats != null)
            foreach (var m in runtimeMats) DestroyImmediate(m);
    }

    /*======================== ���t���[�� ========================*/

    void LateUpdate()
    {
        Camera src = Camera.main;
        if (!src || !mirrorCam) return;

        /*----- 1) ���C���J�����ݒ�R�s�[ -----*/
        mirrorCam.CopyFrom(src);
        mirrorCam.targetTexture = rt;

        /*----- 2) ���ʕ��ʂ����[���h�Ŏ擾 -----*/
        Vector3 pos = transform.position;
        Vector3 normal = transform.TransformDirection(localNormal).normalized;
        Plane plane = new Plane(normal, pos);

        /*----- 3) ���˃r���[�s��v�Z -----*/
        mirrorCam.worldToCameraMatrix = CalcReflectionMatrix(plane) * src.worldToCameraMatrix;

        /*----- 4) ���s���e�Ή� -----*/
        if (src.orthographic)
        {
            mirrorCam.orthographic = true;
            mirrorCam.orthographicSize = src.orthographicSize;
        }

        /*----- 5) Oblique �N���b�s���O�ŗ��ʏ��� -----*/
        Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z,
                                        -Vector3.Dot(normal, pos));
        var proj = src.projectionMatrix;
        mirrorCam.projectionMatrix =
            proj * CalculateObliqueMatrix(proj, clipPlane);

        /*----- 6) ���]�`�� (Cull Front) -----*/
        GL.invertCulling = true;
        mirrorCam.Render();
        GL.invertCulling = false;
    }

    /*====================== ���[�e�B���e�B ======================*/

    // ���ˍs��
    static Matrix4x4 CalcReflectionMatrix(Plane p)
    {
        Vector3 n = p.normal;
        float d = -p.distance;
        float nx = -2f * n.x, ny = -2f * n.y, nz = -2f * n.z;

        return new Matrix4x4
        {
            m00 = 1f + nx * n.x,
            m01 = nx * n.y,
            m02 = nx * n.z,
            m03 = nx * d,
            m10 = ny * n.x,
            m11 = 1f + ny * n.y,
            m12 = ny * n.z,
            m13 = ny * d,
            m20 = nz * n.x,
            m21 = nz * n.y,
            m22 = 1f + nz * n.z,
            m23 = nz * d,
            m30 = 0,
            m31 = 0,
            m32 = 0,
            m33 = 1
        };
    }

    // Oblique Clip
    static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 proj, Vector4 plane)
    {
        Vector4 q = new Vector4(
            (Mathf.Sign(plane.x) + proj.m02) / proj.m00,
            (Mathf.Sign(plane.y) + proj.m12) / proj.m11,
            -1f,
            (1f + proj.m22) / proj.m23
        );
        Vector4 c = plane * (2f / Vector4.Dot(plane, q));

        proj.m20 = c.x;
        proj.m21 = c.y;
        proj.m22 = c.z + 1f;
        proj.m23 = c.w;
        return proj;
    }

    // �}�e���A���̃e�N�X�`���X���b�g�� RT �ɍ����ւ�
    static void ReplaceTextureSlot(Material m, RenderTexture rt)
    {
        if (m.HasProperty(MainTexId))
            m.SetTexture(MainTexId, rt);
        if (m.HasProperty(BaseMapId))
            m.SetTexture(BaseMapId, rt);
    }
}
