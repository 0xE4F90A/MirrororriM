// Assets/Scripts/MirrorOrthographic.cs
using UnityEngine;

/// <summary>
/// ────────────────────────────────────────────────────────────────────
///  📌 Orthographic Mirror コンポーネント（Built-in RP 用）
///      ・Quad／Plane どちらでも使える（Inspector で法線を選択）
///      ・鏡専用カメラ＋RenderTexture を自動生成し、_MainTex に流し込むだけ
///      ※Attach するだけで動きます
/// ────────────────────────────────────────────────────────────────────
/// </summary>
[ExecuteAlways, RequireComponent(typeof(Renderer))]
public sealed class MirrorOrthographic : MonoBehaviour
{
    // ────────── Inspector で調整できる項目 ──────────
    public enum NormalAxis { Forward, Up }               // 表面法線

    [Header("基本設定")]
    [SerializeField] int textureSize = 1024;  // RenderTexture 解像度
    [SerializeField] LayerMask reflectionMask = ~0;    // 映すレイヤ
    [SerializeField] float clipPlaneOffset = 0.03f; // クリップ面微オフセット
    [SerializeField] NormalAxis normalAxis = NormalAxis.Forward; // Quad 用

    // ────────── 内部オブジェクト ──────────
    Camera mirrorCam;
    RenderTexture mirrorRT;
    static readonly int MainTex = Shader.PropertyToID("_MainTex");

    // =================================================================
    // Unity 標準メソッド
    // =================================================================
    void OnEnable() => Init();
    void OnDisable() => Cleanup();

    void LateUpdate()
    {
        if (!mirrorCam) return;
        Camera main = Camera.main;
        if (!main) return;

        // ── メインカメラ設定をコピー ──
        mirrorCam.orthographic = true;
        mirrorCam.orthographicSize = main.orthographicSize;
        mirrorCam.aspect = main.aspect;
        mirrorCam.cullingMask = reflectionMask;
        mirrorCam.nearClipPlane = main.nearClipPlane;
        mirrorCam.farClipPlane = main.farClipPlane;

        // ── 鏡面法線 ──
        Vector3 nWS =
            normalAxis == NormalAxis.Forward ? transform.forward : transform.up;
        nWS.Normalize();
        float d = -Vector3.Dot(nWS, transform.position);           // 平面方程式: n·x + d = 0

        // ── 反射行列（world→reflected world）──
        Matrix4x4 R = Matrix4x4.identity;
        R.m00 = 1 - 2 * nWS.x * nWS.x; R.m01 = -2 * nWS.x * nWS.y; R.m02 = -2 * nWS.x * nWS.z; R.m03 = -2 * d * nWS.x;
        R.m10 = -2 * nWS.y * nWS.x; R.m11 = 1 - 2 * nWS.y * nWS.y; R.m12 = -2 * nWS.y * nWS.z; R.m13 = -2 * d * nWS.y;
        R.m20 = -2 * nWS.z * nWS.x; R.m21 = -2 * nWS.z * nWS.y; R.m22 = 1 - 2 * nWS.z * nWS.z; R.m23 = -2 * d * nWS.z;

        // ── View/Projection 行列を直接セット ──
        Matrix4x4 view = main.worldToCameraMatrix * R;
        mirrorCam.worldToCameraMatrix = view;

        // Transform を同期（Gizmos/Frustum 判定用）
        Matrix4x4 invView = view.inverse;
        mirrorCam.transform.SetPositionAndRotation(invView.GetColumn(3), invView.rotation);

        // ── 斜めクリップ平面（Camera 空間）──
        Vector4 planeWS = new Vector4(nWS.x, nWS.y, nWS.z,
                                      -Vector3.Dot(nWS, transform.position) - clipPlaneOffset);
        Vector4 planeCS = mirrorCam.worldToCameraMatrix * planeWS;

        Matrix4x4 proj = main.projectionMatrix;
        MakeProjectionOblique(ref proj, planeCS);
        mirrorCam.projectionMatrix = proj;

        // ── 描画 ──
        GL.invertCulling = true;   // 反射で裏表が逆になるため
        mirrorCam.Render();
        GL.invertCulling = false;
    }

    // =================================================================
    // 初期化
    // =================================================================
    void Init()
    {
        if (mirrorCam) return;

        // ── RenderTexture ──
        mirrorRT = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = $"RT_{name}_Mirror",
            hideFlags = HideFlags.HideAndDontSave
        };

        // ── マテリアル確保 ──
        Renderer rend = GetComponent<Renderer>();
        Material mat = rend.sharedMaterial;

        if (mat == null || mat.shader.name != "Unlit/MirrorOrtho")
        {
            Shader sh = Shader.Find("Unlit/MirrorOrtho");
            if (!sh)
            {
                Debug.LogError("❌ Shader \"Unlit/MirrorOrtho\" not found.");
                enabled = false;
                return;
            }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            rend.sharedMaterial = mat;
        }

        // ★ ここで RT と Tiling を設定（mat が必ず存在している状態）
        mat.SetTexture(MainTex, mirrorRT);

        mat.mainTextureScale =
            (normalAxis == NormalAxis.Up) ? new Vector2(0.1f, 0.1f)   // Plane
                                          : Vector2.one;              // Quad

        // ── 鏡カメラ生成 ──
        GameObject go = new GameObject("MirrorCam")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        go.transform.parent = transform;
        mirrorCam = go.AddComponent<Camera>();
        mirrorCam.enabled = false;
        mirrorCam.targetTexture = mirrorRT;
    }


    // =================================================================
    // 破棄
    // =================================================================
    void Cleanup()
    {
        if (mirrorRT)
        {
            if (Application.isPlaying) Destroy(mirrorRT);
            else DestroyImmediate(mirrorRT);
        }
        if (mirrorCam)
        {
            if (Application.isPlaying) Destroy(mirrorCam.gameObject);
            else DestroyImmediate(mirrorCam.gameObject);
        }
        mirrorRT = null;
        mirrorCam = null;
    }

    // =================================================================
    // 斜めクリップ平面合成
    // =================================================================
    static void MakeProjectionOblique(ref Matrix4x4 proj, Vector4 planeCS)
    {
        Vector4 q = proj.inverse * new Vector4(
            Sign(planeCS.x), Sign(planeCS.y), 1.0f, 1.0f);

        Vector4 c = planeCS * (2.0f / Vector4.Dot(planeCS, q));

        proj[2] = c.x - proj[3];
        proj[6] = c.y - proj[7];
        proj[10] = c.z - proj[11];
        proj[14] = c.w - proj[15];
    }
    static float Sign(float v) => v < 0 ? -1f : 1f;
}
