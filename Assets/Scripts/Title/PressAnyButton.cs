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
    [SerializeField] private bool m_LoadOnAnyKey = false;
    [SerializeField] private KeyCode m_TriggerKey = KeyCode.Return;

    [Header("Exit メニュー参照（未設定なら自動探索）")]
    [SerializeField] private ToggleExit m_ToggleExit;

    [Header("メニュー閉鎖直後の入力抑止時間(秒)")]
    [SerializeField] private float m_IgnoreSecondsAfterMenuClose = 0.15f;

    // 内部状態
    private bool m_LastMenuVisible;
    private float m_InputResumeTime; // この時刻までは入力無視

    private void Awake()
    {
        if (m_TargetImage == null)
            m_TargetImage = GetComponent<RawImage>();

        if (m_ToggleExit == null)
        {
            m_ToggleExit = GetComponent<ToggleExit>()
                         ?? GetComponentInParent<ToggleExit>()
                         ?? GetComponentInChildren<ToggleExit>();
            if (m_ToggleExit == null)
                Debug.LogWarning("PressAnyButton: ToggleExit が見つかりません。Exit メニューの可視状態は無視します。");
        }

        // 初期の可視状態を記録
        m_LastMenuVisible = (m_ToggleExit != null && m_ToggleExit.GetVisible());
        m_InputResumeTime = 0f;
    }

    private void Update()
    {
        LoopFade();

        bool menuVisible = (m_ToggleExit != null && m_ToggleExit.GetVisible());

        // 可視→非可視 に切り替わった「瞬間」を検知して入力を抑止
        if (m_LastMenuVisible && !menuVisible)
        {
            m_InputResumeTime = Time.unscaledTime + Mathf.Max(0f, m_IgnoreSecondsAfterMenuClose);
        }
        m_LastMenuVisible = menuVisible;

        // メニュー表示中は何もしない
        if (menuVisible)
            return;

        // 閉じた直後の入力は捨てる（同フレーム含む）
        if (Time.unscaledTime < m_InputResumeTime)
            return;

        bool triggered = m_LoadOnAnyKey ? Input.anyKeyDown || PadBool.AnyDown() : Input.GetKeyDown(m_TriggerKey) || PadBool.IsADown();
        if (triggered && !string.IsNullOrEmpty(m_SceneName))
        {
            SceneManager.LoadScene(m_SceneName);
        }
    }

    private void LoopFade()
    {
        if (m_TargetImage == null) return;

        float minA = Mathf.Min(m_MinAlpha, m_MaxAlpha);
        float maxA = Mathf.Max(m_MinAlpha, m_MaxAlpha);

        float t = Mathf.PingPong(Time.time * Mathf.Max(0f, m_FadeSpeed), 1f);
        float alpha = Mathf.Lerp(minA, maxA, t);

        Color c = m_TargetImage.color;
        c.a = alpha;
        m_TargetImage.color = c;
    }
}
