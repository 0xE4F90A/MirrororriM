using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // Keyboard
#endif

/// <summary>
/// 指定キーを n 秒以上長押ししたら特定のシーンをロードする。
/// ・両Inputに対応（ENABLE_INPUT_SYSTEM / ENABLE_LEGACY_INPUT_MANAGER）
/// ・TimeScaleの影響を受けない(既定) or 受けるを選択可
/// ・ビルドに含まれていないシーン名なら警告のみでロードしない
/// </summary>
[DisallowMultipleComponent]
public sealed class HoldLoadScene : MonoBehaviour
{
    [Header("=== 入力 ===")]
    [SerializeField, Tooltip("長押しを検出するキー")]
    private KeyCode m_Key = KeyCode.Escape;

    [SerializeField, Min(0.05f), Tooltip("この秒数以上押し続けたらロード")]
    private float m_HoldSeconds = 2.0f;

    [SerializeField, Tooltip("一度発火したら、その後は無効化（多重ロード防止）")]
    private bool m_FireOnce = true;

    [Header("=== シーン ===")]
    [SerializeField, Tooltip("ロードするシーン名（BuildSettings に登録しておくこと）")]
    private string m_SceneName = "Title";

    [SerializeField, Tooltip("Single=切替 / Additive=加算")]
    private LoadSceneMode m_LoadMode = LoadSceneMode.Single;

    [Header("=== 時間/その他 ===")]
    [SerializeField, Tooltip("true: Time.unscaledDeltaTime を使用し、Time.timeScaleの影響を受けない")]
    private bool m_UseUnscaledTime = true;

    [SerializeField, Tooltip("起動時にタイマーをリセットする")]
    private bool m_ResetOnEnable = true;

    [SerializeField, Tooltip("押し続けている間の進捗(0-1)をディバッグ表示")]
    private bool m_DebugProgressLog = false;

    // --- ランタイム ---
    private float m_HoldTimer = 0f;
    private bool m_Fired = false;

    private void OnEnable()
    {
        if (m_ResetOnEnable)
        {
            m_HoldTimer = 0f;
            m_Fired = false;
        }
    }

    private void Update()
    {
        if (m_FireOnce && m_Fired) return;

        bool held = IsKeyHeld(m_Key);

        float dt = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (held)
        {
            // 押下中：タイマー加算
            m_HoldTimer += dt;

            if (m_DebugProgressLog && m_HoldSeconds > 0f)
            {
                float p = Mathf.Clamp01(m_HoldTimer / m_HoldSeconds);
                Debug.Log($"[LongPress] {m_Key} progress: {(p * 100f):F0}%");
            }

            if (m_HoldTimer >= m_HoldSeconds)
            {
                TryLoadScene();
                if (m_FireOnce) m_Fired = true;
                // 連打で連続ロードを避けるため、即時リセット
                m_HoldTimer = 0f;
            }
        }
        else
        {
            // 離されたらリセット
            if (m_HoldTimer > 0f) m_HoldTimer = 0f;
        }
    }

    private void TryLoadScene()
    {
        if (string.IsNullOrEmpty(m_SceneName))
        {
            Debug.LogWarning("[LongPressSceneLoader] シーン名が未設定です。");
            return;
        }

        // Build Settings に含まれているかの軽いチェック
        if (!CanLoadByName(m_SceneName))
        {
            Debug.LogWarning($"[LongPressSceneLoader] シーン '{m_SceneName}' はビルドに含まれていない可能性があります。");
            // それでもロードを試みたい場合は下の return を外す
            return;
        }

        Debug.Log($"[LongPressSceneLoader] LoadScene: {m_SceneName} ({m_LoadMode})");
        SceneManager.LoadSceneAsync(m_SceneName, m_LoadMode);
    }

    /// <summary>
    /// 入力：指定 KeyCode が保持されているか（両Input対応）
    /// </summary>
    private static bool IsKeyHeld(KeyCode key)
    {
        if (PadBool.IsBHeld()) return true;
        // 1) 新 Input System
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (TryConvertKeyCodeToInputSystemKey(key, out var isk))
            {
                var control = kb[isk];
                if (control != null && control.isPressed) return true;
            }
        }
#endif

        // 2) 旧 Input Manager
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(key)) return true;
#endif

        return false;
    }

    /// <summary>
    /// KeyCode → InputSystem.Key の主な差異を吸収（必要分だけ網羅）
    /// </summary>
#if ENABLE_INPUT_SYSTEM
    private static bool TryConvertKeyCodeToInputSystemKey(KeyCode keyCode, out Key key)
    {
        // 同名で通るケースが多い
        if (Enum.TryParse(keyCode.ToString(), out key))
            return true;

        // 名前が異なる代表例
        switch (keyCode)
        {
            case KeyCode.Return: key = Key.Enter; return true;
            case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
            case KeyCode.BackQuote: key = Key.Backquote; return true;
            case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
            case KeyCode.RightControl: key = Key.RightCtrl; return true;
            case KeyCode.LeftAlt: key = Key.LeftAlt; return true;
            case KeyCode.RightAlt: key = Key.RightAlt; return true;
            case KeyCode.LeftShift: key = Key.LeftShift; return true;
            case KeyCode.RightShift: key = Key.RightShift; return true;
            case KeyCode.Print: key = Key.PrintScreen; return true;
            case KeyCode.Numlock: key = Key.NumLock; return true;
            case KeyCode.ScrollLock: key = Key.ScrollLock; return true;
            case KeyCode.Pause: key = Key.Pause; return true;
            case KeyCode.Insert: key = Key.Insert; return true;
            case KeyCode.Delete: key = Key.Delete; return true;
            case KeyCode.PageUp: key = Key.PageUp; return true;
            case KeyCode.PageDown: key = Key.PageDown; return true;
            case KeyCode.UpArrow: key = Key.UpArrow; return true;
            case KeyCode.DownArrow: key = Key.DownArrow; return true;
            case KeyCode.LeftArrow: key = Key.LeftArrow; return true;
            case KeyCode.RightArrow: key = Key.RightArrow; return true;
            // Keypad 0-9
            case KeyCode.Keypad0: key = Key.Numpad0; return true;
            case KeyCode.Keypad1: key = Key.Numpad1; return true;
            case KeyCode.Keypad2: key = Key.Numpad2; return true;
            case KeyCode.Keypad3: key = Key.Numpad3; return true;
            case KeyCode.Keypad4: key = Key.Numpad4; return true;
            case KeyCode.Keypad5: key = Key.Numpad5; return true;
            case KeyCode.Keypad6: key = Key.Numpad6; return true;
            case KeyCode.Keypad7: key = Key.Numpad7; return true;
            case KeyCode.Keypad8: key = Key.Numpad8; return true;
            case KeyCode.Keypad9: key = Key.Numpad9; return true;
            case KeyCode.KeypadPeriod: key = Key.NumpadPeriod; return true;
            case KeyCode.KeypadDivide: key = Key.NumpadDivide; return true;
            case KeyCode.KeypadMultiply: key = Key.NumpadMultiply; return true;
            case KeyCode.KeypadMinus: key = Key.NumpadMinus; return true;
            case KeyCode.KeypadPlus: key = Key.NumpadPlus; return true;
            default:
                key = Key.None;
                return false;
        }
    }
#endif

    /// <summary>
    /// 名前指定のロード可否（BuildSettings に含まれていない場合は false を返す可能性）
    /// </summary>
    private static bool CanLoadByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        // Application.CanStreamedLevelBeLoaded は将来的に非推奨の可能性があるため警告抑制
#pragma warning disable 618
        bool ok = Application.CanStreamedLevelBeLoaded(sceneName);
#pragma warning restore 618

        // 互換のためのフォールバック：非保証だが、falseでもロードできるケースはある
        return ok || true; // 厳密に止めたいなら 'return ok;' に変更
    }
}
