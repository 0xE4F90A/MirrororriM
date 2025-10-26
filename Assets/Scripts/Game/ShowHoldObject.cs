using UnityEngine;

[AddComponentMenu("Game/UI/Show On Key Hold")]
[DisallowMultipleComponent]
public sealed class ShowHoldObject : MonoBehaviour
{
    [Header("表示に使うキー（旧 Input Manager / Both で動作）")]
    [SerializeField] private KeyCode m_Key = KeyCode.E;

    [Header("表示/非表示の対象（1つ以上）")]
    [SerializeField] private GameObject[] m_Targets;

    [Header("起動時は非表示にする")]
    [SerializeField] private bool m_HideOnAwake = true;

    private bool m_IsShown;

    private void Awake()
    {
        if (m_HideOnAwake)
        {
            SetActiveAll(false);
            m_IsShown = false;
        }
        else
        {
            m_IsShown = IsAnyActive();
        }
    }

    private void Update()
    {
        bool shouldShow = Input.GetKey(m_Key) || PadBool.IsR3Held() || PadBool.IsL3Held();

        if (shouldShow != m_IsShown)
        {
            SetActiveAll(shouldShow);
            m_IsShown = shouldShow;
        }
    }

    private void SetActiveAll(bool active)
    {
        if (m_Targets == null) return;

        for (int i = 0; i < m_Targets.Length; i++)
        {
            GameObject go = m_Targets[i];
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }

    private bool IsAnyActive()
    {
        if (m_Targets == null) return false;
        for (int i = 0; i < m_Targets.Length; i++)
        {
            GameObject go = m_Targets[i];
            if (go != null && go.activeSelf) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        m_HideOnAwake = true; // デフォルト非表示
    }
#endif
}
