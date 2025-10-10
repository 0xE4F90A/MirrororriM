using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PressAnyButton : MonoBehaviour
{
    [Header("フェード対象 RawImage")]
    [SerializeField] private RawImage m_TargetImage;

    [Header("フェード設定")]
    [SerializeField] private float m_FadeSpeed = 1.0f;
    [SerializeField, Range(0f, 1f)] private float m_MinAlpha = 0.6f;
    [SerializeField, Range(0f, 1f)] private float m_MaxAlpha = 1.0f;

    [Header("遷移先シーン名")]
    [SerializeField] private string m_SceneName = "SelectScene";

    [Header("入力設定")]
    [SerializeField] private bool m_LoadOnAnyKey = true;   // true で任意キー、false で特定キー
    [SerializeField] private KeyCode m_TriggerKey = KeyCode.Return;

    private void Awake()
    {
        // インスペクター未設定なら同一GameObjectから取得
        if (m_TargetImage == null)
        {
            m_TargetImage = GetComponent<RawImage>();
        }
    }

    private void Update()
    {
        LoopFade();

        bool triggered = m_LoadOnAnyKey ? Input.anyKeyDown : Input.GetKeyDown(m_TriggerKey);
        if (triggered && !string.IsNullOrEmpty(m_SceneName))
        {
            SceneManager.LoadScene(m_SceneName);
        }
    }

    private void LoopFade()
    {
        if (m_TargetImage == null) { return; }

        // 誤設定で Min > Max の場合にも破綻しないように正規化
        float minA = Mathf.Min(m_MinAlpha, m_MaxAlpha);
        float maxA = Mathf.Max(m_MinAlpha, m_MaxAlpha);

        float t = Mathf.PingPong(Time.time * Mathf.Max(0f, m_FadeSpeed), 1f);
        float alpha = Mathf.Lerp(minA, maxA, t);

        Color c = m_TargetImage.color;
        c.a = alpha;
        m_TargetImage.color = c;
    }
}
