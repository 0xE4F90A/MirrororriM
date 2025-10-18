using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EnterToScene : MonoBehaviour
{
    [Serializable]
    private sealed class TargetBinding
    {
        [Header("距離判定対象")]
        [SerializeField] private Transform m_Target;

        [Header("半径 [m]（既定 0.5）")]
        [SerializeField][Min(0f)] private float m_RadiusMeters = 0.5f;

        [Header("遷移先シーン（名前優先）")]
        [SerializeField] private bool m_UseSceneName = true;
        [SerializeField] private string m_SceneName = string.Empty;
        [SerializeField] private int m_SceneBuildIndex = -1;

        public Transform Target => m_Target;
        public float Radius => (m_RadiusMeters <= 0f) ? 0.5f : m_RadiusMeters;

        public bool TryResolveScene(out string o_SceneName, out int o_BuildIndex)
        {
            o_SceneName = default;
            o_BuildIndex = -1;

            if (m_UseSceneName)
            {
                if (string.IsNullOrEmpty(m_SceneName)) return false;
                if (!Application.CanStreamedLevelBeLoaded(m_SceneName)) return false;
                o_SceneName = m_SceneName;
                return true;
            }
            else
            {
                if (m_SceneBuildIndex < 0 || m_SceneBuildIndex >= SceneManager.sceneCountInBuildSettings) return false;
                o_BuildIndex = m_SceneBuildIndex;
                return true;
            }
        }

        public void Load(bool i_Async, LoadSceneMode i_Mode)
        {
            if (!TryResolveScene(out string l_Name, out int l_Index))
            {
                Debug.LogError($"[ProximityEnterMultiScene] 無効なシーン指定（Target={m_Target?.name ?? "null"}）。Build Settings/名前を確認してください。");
                return;
            }

            if (i_Async)
            {
                if (!string.IsNullOrEmpty(l_Name)) SceneManager.LoadSceneAsync(l_Name, i_Mode);
                else SceneManager.LoadSceneAsync(l_Index, i_Mode);
            }
            else
            {
                if (!string.IsNullOrEmpty(l_Name)) SceneManager.LoadScene(l_Name, i_Mode);
                else SceneManager.LoadScene(l_Index, i_Mode);
            }
        }
    }

    [Header("基準 BaseModel（Transform）")]
    [SerializeField] private Transform m_BaseModel;

    [Header("対象ごとの距離と遷移先（上から優先）")]
    [SerializeField] private TargetBinding[] m_Bindings = Array.Empty<TargetBinding>();

    [Header("入力設定")]
    [SerializeField] private KeyCode m_EnterKey = KeyCode.Return;
    [SerializeField] private bool m_AcceptKeypadEnter = true;

    [Header("複数ヒット時の優先（false: 上から, true: 最短距離）")]
    [SerializeField] private bool m_PreferNearest = false;

    [Header("ロード設定")]
    [SerializeField] private bool m_Async = false;
    [SerializeField] private LoadSceneMode m_LoadMode = LoadSceneMode.Single;

    private void Update()
    {
        if (!IsEnterPressedThisFrame())
        {
            return;
        }

        int l_Index = FindHitBindingIndex();
        if (l_Index < 0)
        {
            return; // どれにも入っていない
        }

        m_Bindings[l_Index].Load(m_Async, m_LoadMode);
    }

    private int FindHitBindingIndex()
    {
        if (m_BaseModel == null || m_Bindings == null || m_Bindings.Length == 0)
        {
            return -1;
        }

        Vector3 l_BasePos = m_BaseModel.position;
        int l_FirstHit = -1;
        float l_BestSq = float.PositiveInfinity;
        int l_BestIdx = -1;

        for (int i = 0; i < m_Bindings.Length; ++i)
        {
            TargetBinding l_B = m_Bindings[i];
            if (l_B == null || l_B.Target == null)
            {
                continue;
            }

            float l_Sq = (l_BasePos - l_B.Target.position).sqrMagnitude;
            float l_R = l_B.Radius;
            if (l_Sq <= l_R * l_R)
            {
                if (l_FirstHit < 0) l_FirstHit = i;
                if (m_PreferNearest && l_Sq < l_BestSq)
                {
                    l_BestSq = l_Sq;
                    l_BestIdx = i;
                }
            }
        }

        return m_PreferNearest ? l_BestIdx : l_FirstHit;
    }

    private bool IsEnterPressedThisFrame()
    {
        bool l_Pressed = Input.GetKeyDown(m_EnterKey) || PadBool.IsADown();
        if (!l_Pressed && m_AcceptKeypadEnter)
        {
            l_Pressed |= Input.GetKeyDown(KeyCode.KeypadEnter);
        }
        return l_Pressed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (m_Bindings == null) m_Bindings = Array.Empty<TargetBinding>();
    }

    private void OnDrawGizmosSelected()
    {
        if (m_Bindings == null) return;
        for (int i = 0; i < m_Bindings.Length; ++i)
        {
            var b = m_Bindings[i];
            if (b == null || b.Target == null) continue;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(b.Target.position, b.Radius);
        }
    }
#endif
}
