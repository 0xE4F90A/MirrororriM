using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BackgroundFadePos : MonoBehaviour
{
    [System.Serializable]
    public sealed class FadeRule
    {
        [Tooltip("フェード対象の RawImage")]
        public RawImage Image;

        [Header("トリガ（ワールドX）")]
        [Tooltip("このX以下に入った瞬間に 1→0 フェード開始（例：-10）")]
        public float FadeOutAtX = -10f;

        [Tooltip("このX以上に戻った瞬間に 0→1 フェード開始（例：-9）※対象のαが0付近の時のみ")]
        public float FadeInAtX = -9f;

        [Header("時間（秒）")]
        public float FadeOutDuration = 0.5f;
        public float FadeInDuration = 0.5f;
    }

    [Header("=== 監視対象 ===")]
    [SerializeField] private Transform m_Base; // 未設定なら自身

    [Header("=== ルール（複数可） ===")]
    [SerializeField] private FadeRule[] m_Rules;

    [Header("=== オプション ===")]
    [Tooltip("true: Time.unscaledDeltaTime でフェード（ポーズ中も動く）")]
    [SerializeField] private bool m_UseUnscaledTime = false;

    [Tooltip("αを0とみなす誤差（復帰トリガのゲート）")]
    [SerializeField] private float m_AlphaZeroEps = 0.01f;

    private float m_PrevX;
    private readonly Dictionary<RawImage, Coroutine> m_FadeRoutines = new();

    private void Awake()
    {
        if (m_Base == null) m_Base = transform;
        m_PrevX = m_Base.position.x;
    }

    private void OnDisable()
    {
        // 実行中のフェードを停止
        foreach (var kv in m_FadeRoutines)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        m_FadeRoutines.Clear();
    }

    private void Update()
    {
        if (m_Rules == null || m_Rules.Length == 0 || m_Base == null) return;

        float x = m_Base.position.x;

        for (int i = 0; i < m_Rules.Length; ++i)
        {
            var r = m_Rules[i];
            if (r == null || r.Image == null) continue;

            // --- OUT: （前フレーム > 閾値）かつ（今フレーム <= 閾値）で発火 ---
            if (m_PrevX > r.FadeOutAtX && x <= r.FadeOutAtX)
            {
                StartFade(r.Image, targetAlpha: 0f, r.FadeOutDuration);
            }

            // --- IN: αが0付近 かつ （前フレーム < 閾値）かつ（今フレーム >= 閾値）で発火 ---
            if (GetAlpha(r.Image) <= m_AlphaZeroEps && m_PrevX < r.FadeInAtX && x >= r.FadeInAtX)
            {
                StartFade(r.Image, targetAlpha: 1f, r.FadeInDuration);
            }
        }

        m_PrevX = x;
    }

    // 現在のフェードを止めて新しいフェードを開始
    private void StartFade(RawImage img, float targetAlpha, float duration)
    {
        if (img == null) return;

        if (m_FadeRoutines.TryGetValue(img, out var co) && co != null)
        {
            StopCoroutine(co);
        }
        m_FadeRoutines[img] = StartCoroutine(FadeRoutine(img, targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(RawImage img, float targetAlpha, float duration)
    {
        Color c = img.color;
        float start = c.a;
        float t = 0f;
        if (duration <= 0f)
        {
            c.a = Mathf.Clamp01(targetAlpha);
            img.color = c;
            yield break;
        }

        while (t < duration)
        {
            t += m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // 緩やかな補間（イージング：SmoothStep）
            float a = Mathf.SmoothStep(start, targetAlpha, u);
            c.a = a;
            img.color = c;
            yield return null;
        }
        c.a = Mathf.Clamp01(targetAlpha);
        img.color = c;
        m_FadeRoutines[img] = null;
    }

    private static float GetAlpha(RawImage img) => (img != null) ? img.color.a : 0f;
}
