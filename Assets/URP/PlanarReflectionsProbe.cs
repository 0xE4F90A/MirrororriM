using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways, AddComponentMenu("Rendering/Planar Reflections Probe")]
public class PlanarReflectionsProbe : MonoBehaviour
{
    //========================
    // 追加: 法線入力モード & 軸選択
    //========================
    public enum NormalInputMode
    {
        TransformAxis,   // transform の任意軸を法線に
        CustomAnglesDeg, // 角度(-180..180)指定 → ベクトル化
        CustomVector     // 既存の Vector3 指定（正規化）
    }

    public enum NormalSourceAxis
    {
        Forward,
        Up,
        Right
    }

    [System.Serializable]
    public struct Angle3Deg
    {
        [Range(-180f, 180f)] public float X;
        [Range(-180f, 180f)] public float Y;
        [Range(-180f, 180f)] public float Z;

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    //========================
    // 既存設定
    //========================
    [Range(1, 4)] public int targetTextureID = 1;
    [Space(10)]
    [Tooltip("旧: 生ベクトルで法線を与える（-1..1）。新仕様では下のNormalInputMode=CustomVector時のみ参照。")]
    public bool useCustomNormal = false;

    [Tooltip("旧: 生ベクトルでの法線（-1..1）。NormalInputMode=CustomVector時のみ使用。")]
    public Vector3 customNormal;

    [Space(10)]
    [Range(0.01f, 1.0f)] public float reflectionsQuality = 1f;
    public float farClipPlane = 1000;
    public bool renderBackground = true;
    [Space(10)]
    public bool renderInEditor = false;

    //========================
    // 追加設定
    //========================
    [Header("法線入力モード")]
    [Tooltip("TransformAxis: このオブジェクトの任意軸をそのまま法線にする\nCustomAnglesDeg: 角度(-180..180°)で入力し、方向に変換\nCustomVector: 生Vector3（-1..1）を正規化して使用")]
    public NormalInputMode normalInputMode = NormalInputMode.TransformAxis;

    [Tooltip("どのローカル軸を法線として使うか（TransformAxis/CustomAnglesDeg両方で使用）")]
    public NormalSourceAxis normalAxis = NormalSourceAxis.Forward;

    [Header("角度(-180..180°)での法線指定（normalInputMode=CustomAnglesDeg）")]
    [Tooltip("例: X=0, Y=0, Z=-90 と入れると、Quaternion.Euler(0,0,-90)で基準軸を回してベクトル化します")]
    public Angle3Deg customNormalAnglesDeg;

    //========================
    // 内部
    //========================
    public GameObject _probeGO;
    public Camera _probe;
    private Skybox _probeSkybox;
    private Dictionary<Camera, RenderTexture> _camTextureMap =
        new Dictionary<Camera, RenderTexture>();
    private ArrayList _ignoredCameras = new ArrayList();

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += PreRender;
    }

    private void OnDisable()
    {
        FinalizeProbe();
        RenderPipelineManager.beginCameraRendering -= PreRender;
    }

    private void OnDestroy()
    {
        FinalizeProbe();
        RenderPipelineManager.beginCameraRendering -= PreRender;
    }

    private void InitializeProbe()
    {
        _probeGO = new GameObject("", typeof(Camera), typeof(Skybox));
        _probeGO.name = "PRCamera" + _probeGO.GetInstanceID().ToString();
        _probeGO.hideFlags = HideFlags.HideAndDontSave;
        _probe = _probeGO.GetComponent<Camera>();
        _probeSkybox = _probeGO.GetComponent<Skybox>();
        _probeSkybox.enabled = false;
        _probeSkybox.material = null;
    }

    private void FinalizeProbe()
    {
        if (_probe == null) return;

        if (Application.isEditor)
        {
            Object.DestroyImmediate(_probeGO);
        }
        else
        {
            Object.Destroy(_probeGO);
        }
    }

    private void CleanupRenderTextures()
    {
        foreach (RenderTexture texture in _camTextureMap.Values)
        {
            texture.Release();
        }
        _camTextureMap.Clear();
    }

    private bool CheckCamera(Camera cam)
    {
        if (cam.cameraType == CameraType.Reflection) return true;
        else if (!renderInEditor && cam.cameraType == CameraType.SceneView) return true;
        else if (_ignoredCameras.Contains(cam)) return true;
        return false;
    }

    private void PreRender(ScriptableRenderContext context, Camera cam)
    {
        if (CheckCamera(cam))
        {
            return;
        }
        else if (_probe == null)
        {
            InitializeProbe();
        }

        Vector3 normal = GetNormal(); // ここが新仕様に対応
        UpdateProbeSettings(cam);
        CreateRenderTexture(cam);
        UpdateProbeTransform(cam, normal);
        CalculateObliqueProjection(normal);
        UniversalRenderPipeline.RenderSingleCamera(context, _probe);

        string texName = "_PlanarReflectionsTex" + targetTextureID.ToString();
        _probe.targetTexture.SetGlobalShaderProperty(texName);
    }

    private void UpdateProbeSettings(Camera cam)
    {
        _probe.CopyFrom(cam);
        _probe.enabled = false;
        _probe.cameraType = CameraType.Reflection;
        _probe.usePhysicalProperties = false;
        _probe.farClipPlane = farClipPlane;
        _probeSkybox.material = null;
        _probeSkybox.enabled = false;

        if (renderBackground)
        {
            _probe.clearFlags = cam.clearFlags;
            if (cam.TryGetComponent(out Skybox camSkybox))
            {
                _probeSkybox.material = camSkybox.material;
                _probeSkybox.enabled = camSkybox.enabled;
            }
        }
        else
        {
            _probe.clearFlags = CameraClearFlags.Nothing;
        }
    }

    private void CreateRenderTexture(Camera cam)
    {
        int width = (int)(cam.pixelWidth * reflectionsQuality);
        int height = (int)(cam.pixelHeight * reflectionsQuality);

        RenderTexture texture = _camTextureMap.GetValueOrDefault(cam, null);
        if (!texture || texture.width != width || texture.height != height)
        {
            if (texture)
            {
                _camTextureMap.Remove(cam);
                texture.Release();
            }
            _probe.targetTexture = new RenderTexture(width, height, 24);
            _probe.targetTexture.Create();
            _camTextureMap.Add(cam, _probe.targetTexture);
        }
        else
        {
            _probe.targetTexture = texture;
        }
    }

    //========================
    // 新: 法線の決定ロジック
    //========================
    private Vector3 GetNormal()
    {
        switch (normalInputMode)
        {
            case NormalInputMode.TransformAxis:
                return GetTransformAxisVector(normalAxis);

            case NormalInputMode.CustomAnglesDeg:
                {
                    Vector3 deg = customNormalAnglesDeg.ToVector3();
                    Vector3 baseAxis = GetBaseAxisVector(normalAxis);
                    Vector3 dir = Quaternion.Euler(deg) * baseAxis;
                    if (dir == Vector3.zero) return Vector3.up; // 安全策
                    return dir.normalized;
                }

            case NormalInputMode.CustomVector:
            default:
                {
                    // 旧仕様の後方互換: useCustomNormal が true なら customNormal を使う
                    if (useCustomNormal)
                    {
                        if (customNormal == Vector3.zero) return Vector3.up;
                        return customNormal.normalized;
                    }
                    // 旧デフォルト: transform.forward
                    return transform.forward;
                }
        }
    }

    private Vector3 GetTransformAxisVector(NormalSourceAxis axis)
    {
        switch (axis)
        {
            case NormalSourceAxis.Up: return transform.up;
            case NormalSourceAxis.Right: return transform.right;
            case NormalSourceAxis.Forward:
            default: return transform.forward;
        }
    }

    private static Vector3 GetBaseAxisVector(NormalSourceAxis axis)
    {
        switch (axis)
        {
            case NormalSourceAxis.Up: return Vector3.up;
            case NormalSourceAxis.Right: return Vector3.right;
            case NormalSourceAxis.Forward:
            default: return Vector3.forward;
        }
    }

    // The probe's camera position should be the current camera's position
    // mirrored by the reflecting plane. Its rotation mirrored too.
    private void UpdateProbeTransform(Camera cam, Vector3 normal)
    {
        Vector3 proj = normal * Vector3.Dot(normal, cam.transform.position - transform.position);
        _probe.transform.position = cam.transform.position - 2f * proj;

        Vector3 probeForward = Vector3.Reflect(cam.transform.forward, normal);
        Vector3 probeUp = Vector3.Reflect(cam.transform.up, normal);
        _probe.transform.LookAt(_probe.transform.position + probeForward, probeUp);
    }

    // The clip plane should coincide with the plane with reflections.
    private void CalculateObliqueProjection(Vector3 normal)
    {
        Matrix4x4 viewMatrix = _probe.worldToCameraMatrix;
        Vector3 viewPosition = viewMatrix.MultiplyPoint(transform.position);
        Vector3 viewNormal = viewMatrix.MultiplyVector(normal);
        Vector4 plane = new Vector4(
            viewNormal.x, viewNormal.y, viewNormal.z,
            -Vector3.Dot(viewPosition, viewNormal));
        _probe.projectionMatrix = _probe.CalculateObliqueMatrix(plane);
    }

    public void IgnoreCamera(Camera cam)
    {
        if (!_ignoredCameras.Contains(cam))
        {
            _ignoredCameras.Add(cam);
        }
    }

    public void UnignoreCamera(Camera cam)
    {
        if (_ignoredCameras.Contains(cam))
        {
            _ignoredCameras.Remove(cam);
        }
    }

    public void ClearIgnoredList()
    {
        _ignoredCameras.Clear();
    }

    public bool IsIgnoring(Camera cam)
    {
        return _ignoredCameras.Contains(cam);
    }

    public static PlanarReflectionsProbe[] FindProbesRenderingTo(int id)
    {
        var probes = FindObjectsOfType<PlanarReflectionsProbe>();
        ArrayList list = new ArrayList();
        foreach (PlanarReflectionsProbe probe in probes)
        {
            if (probe.targetTextureID == id)
            {
                list.Add(probe);
            }
        }
        return (PlanarReflectionsProbe[])list.ToArray(typeof(PlanarReflectionsProbe));
    }

    public static PlanarReflectionsProbe FindProbeRenderingTo(int id)
    {
        var probes = FindObjectsOfType<PlanarReflectionsProbe>();
        foreach (PlanarReflectionsProbe probe in probes)
        {
            if (probe.targetTextureID == id)
            {
                return probe;
            }
        }
        return null;
    }
}
