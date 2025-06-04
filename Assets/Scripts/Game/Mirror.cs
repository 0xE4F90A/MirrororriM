/**************************************************************
 *  Mirror.cs
 *  3D オブジェクトにアタッチするだけで鏡化（Built-in / URP / HDRP）
 *  Author : 0xE4F90A 向け最小コア
 *************************************************************/

using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class Mirror : MonoBehaviour
{
    [Header("鏡が描画する解像度 (正方形)")]
    [SerializeField] int textureSize = 1024;

    [Header("ローカル法線を使う (通常は forward)")]
    [SerializeField] Vector3 localNormal = Vector3.forward;

    Camera mirrorCam;          // 生成した反射カメラ
    RenderTexture rt;                // 鏡用 RT
    Renderer rend;               // 対象 Renderer
    List<Material> runtimeMats;      // 複製した実マテリアル

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"); // URP/HDRP

    /*========================= 初期化 =========================*/

    void OnEnable()
    {
        rend = GetComponent<Renderer>();

        // ① RenderTexture 作成
        rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = $"MirrorRT_{name}",
            antiAliasing = 1,
            hideFlags = HideFlags.DontSave,
            useMipMap = false
        };

        // ② マテリアル複製 ＆ RT を差し替え
        runtimeMats = new List<Material>(rend.sharedMaterials.Length);
        for (int i = 0; i < rend.sharedMaterials.Length; ++i)
        {
            Material inst = Instantiate(rend.sharedMaterials[i]);
            inst.name = rend.sharedMaterials[i].name + " (MirrorInst)";
            ReplaceTextureSlot(inst, rt);
            runtimeMats.Add(inst);
        }
        rend.materials = runtimeMats.ToArray();      // インスタンス配列を適用

        // ③ 非表示カメラ生成
        GameObject camObj = new GameObject($"MirrorCam_{name}");
        camObj.hideFlags = HideFlags.HideAndDontSave;
        mirrorCam = camObj.AddComponent<Camera>();
        mirrorCam.enabled = false;
        mirrorCam.targetTexture = rt;
    }

    /*========================= 後始末 =========================*/

    void OnDisable()
    {
        if (mirrorCam) DestroyImmediate(mirrorCam.gameObject);
        if (rt) DestroyImmediate(rt);
        if (runtimeMats != null)
            foreach (var m in runtimeMats) DestroyImmediate(m);
    }

    /*======================== 毎フレーム ========================*/

    void LateUpdate()
    {
        Camera src = Camera.main;
        if (!src || !mirrorCam) return;

        /*----- 1) メインカメラ設定コピー -----*/
        mirrorCam.CopyFrom(src);
        mirrorCam.targetTexture = rt;

        /*----- 2) 鏡面平面をワールドで取得 -----*/
        Vector3 pos = transform.position;
        Vector3 normal = transform.TransformDirection(localNormal).normalized;
        Plane plane = new Plane(normal, pos);

        /*----- 3) 反射ビュー行列計算 -----*/
        mirrorCam.worldToCameraMatrix = CalcReflectionMatrix(plane) * src.worldToCameraMatrix;

        /*----- 4) 平行投影対応 -----*/
        if (src.orthographic)
        {
            mirrorCam.orthographic = true;
            mirrorCam.orthographicSize = src.orthographicSize;
        }

        /*----- 5) Oblique クリッピングで裏面除去 -----*/
        Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z,
                                        -Vector3.Dot(normal, pos));
        var proj = src.projectionMatrix;
        mirrorCam.projectionMatrix =
            proj * CalculateObliqueMatrix(proj, clipPlane);

        /*----- 6) 反転描画 (Cull Front) -----*/
        GL.invertCulling = true;
        mirrorCam.Render();
        GL.invertCulling = false;
    }

    /*====================== ユーティリティ ======================*/

    // 反射行列
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

    // マテリアルのテクスチャスロットを RT に差し替え
    static void ReplaceTextureSlot(Material m, RenderTexture rt)
    {
        if (m.HasProperty(MainTexId))
            m.SetTexture(MainTexId, rt);
        if (m.HasProperty(BaseMapId))
            m.SetTexture(BaseMapId, rt);
    }
}
