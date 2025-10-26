#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ポーズメニュー:
/// Escapeで開閉。
/// Retry / ExitStage / Back の3つを ↑↓ で選択、Enterで決定。
/// 表示中はポーズ状態（Time.timeScale=0 + IsPaused=true）
/// 決定時はSEを鳴らしてすぐ処理を進める（待たない）
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class PauseMenu : MonoBehaviour
{
    /*========== Inspector ==========*/

    [Header("メニューの表示/非表示を切り替えるキー")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("上下・リセット・決定用キー")]
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode resetKey = KeyCode.Escape;
    [SerializeField] private KeyCode decideKey = KeyCode.Return;

    [Header("各項目(強調表示したいCanvasGroup)")]
    [SerializeField] private CanvasGroup retryGroup;       // "Retry"
    [SerializeField] private CanvasGroup exitStageGroup;   // "ExitStage"
    [SerializeField] private CanvasGroup backGroup;        // "Back"

    [Header("各決定先シーン名（Build Settingsに登録しておく）")]
    [SerializeField] private string retrySceneName;        // Retryで遷移するシーン
    [SerializeField] private string exitStageSceneName;    // ExitStageで遷移するシーン

    [Header("SE 再生設定")]
    [SerializeField] private AudioSource seSource;       // UI用AudioSource（未指定ならAwakeで自動追加）
    [SerializeField] private AudioClip selectSE;         // カーソル移動SE
    [SerializeField] private AudioClip decideSE;         // 決定SE

    /*========== 内部状態 ==========*/

    private CanvasGroup cg;        // メニュー本体のCanvasGroup
    private bool visible;          // メニューが表示中か？
    private bool isPerformingAction; // 決定後の多重入力防止
    private float prevTimeScale = 1.0f; // ポーズ前のtimeScaleを記録


    private int m_UpOnce = 0;
    private int m_DownOnce = 0;

    // 外から確認できる「今ポーズ中か？」
    public static bool IsPaused { get; private set; }

    private enum MenuItem
    {
        Retry = 0,
        ExitStage = 1,
        Back = 2
    }

    // メニューを開いた瞬間の初期選択（とりあえず Back にしておく）
    private MenuItem current = MenuItem.Back;

    /*========== Unity標準メソッド ==========*/

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();

        // 子からCanvasGroupを自動取得（オブジェ名が "Retry", "ExitStage", "Back" である想定）
        if (!retryGroup)
        {
            Transform t = transform.Find("Retry");
            if (t != null) { retryGroup = t.GetComponent<CanvasGroup>(); }
        }
        if (!exitStageGroup)
        {
            Transform t = transform.Find("ExitStage");
            if (t != null) { exitStageGroup = t.GetComponent<CanvasGroup>(); }
        }
        if (!backGroup)
        {
            Transform t = transform.Find("Back");
            if (t != null) { backGroup = t.GetComponent<CanvasGroup>(); }
        }

        // SE用AudioSourceが無ければ自動で用意
        if (!seSource)
        {
            seSource = GetComponent<AudioSource>();
            if (!seSource)
            {
                seSource = gameObject.AddComponent<AudioSource>();
            }
        }
        seSource.playOnAwake = false;
        seSource.spatialBlend = 0.0f; // 2D再生

        // 起動時はメニュー非表示＆ポーズ解除
        SetVisible(false, true);

        // 初期ハイライト反映
        ApplySelectionVisuals();
    }

    private void Update()
    {
        // 決定後は入力をブロック
        if (isPerformingAction)
        {
            return;
        }

        //------------------------------------------------
        // メニューの開閉 (Esc / Start)
        //------------------------------------------------
        if (Input.GetKeyDown(toggleKey) || PadBool.IsStartDown())
        {
            SetVisible(!visible, false);
        }

        // 非表示ならここで終わり
        if (!visible)
        {
            return;
        }


        if (PadBool.IsLeftStickUp())
            ++m_UpOnce;
        else
            m_UpOnce = 0;
        if (PadBool.IsLeftStickDown())
            ++m_DownOnce;
        else
            m_DownOnce = 0;
            //------------------------------------------------
            // 縦方向の選択移動
            //------------------------------------------------
            bool l_UpPressed = Input.GetKeyDown(upKey) || m_UpOnce == 1;
        bool l_DownPressed = Input.GetKeyDown(downKey) || m_DownOnce == 1;
        bool l_ResetPressed = Input.GetKeyDown(resetKey) || PadBool.IsStartDown();
        bool l_DecidePressed = Input.GetKeyDown(decideKey) || PadBool.IsADown();

        // ↑キー：上へ（インデックス -1）
        if (l_UpPressed)
        {
            int l_Index = (int)current;
            if (l_Index > (int)MenuItem.Retry)
            {
                l_Index -= 1;
                current = (MenuItem)l_Index;
                ApplySelectionVisuals();
                PlaySE(selectSE);
            }
        }
        // ↓キー：下へ（インデックス +1）
        else if (l_DownPressed)
        {
            int l_Index = (int)current;
            if (l_Index < (int)MenuItem.Back)
            {
                l_Index += 1;
                current = (MenuItem)l_Index;
                ApplySelectionVisuals();
                PlaySE(selectSE);
            }
        }
        // Reset: とりあえず Back を選ぶ（"戻る"にフォーカスするイメージ）
        else if (l_ResetPressed)
        {
            if (current != MenuItem.Back)
            {
                current = MenuItem.Back;
                ApplySelectionVisuals();
                // 必要なら PlaySE(selectSE);
            }
        }

        //------------------------------------------------
        // 決定
        //------------------------------------------------
        if (l_DecidePressed)
        {
            switch (current)
            {
                case MenuItem.Retry:
                    StartCoroutine(PlayThenLoadScene(retrySceneName));
                    break;

                case MenuItem.ExitStage:
                    StartCoroutine(PlayThenLoadScene(exitStageSceneName));
                    break;

                case MenuItem.Back:
                    // メニューを閉じるだけ
                    PlaySE(decideSE);
                    SetVisible(false, false);
                    break;
            }
        }
    }

    /*========== 表示/非表示とポーズ制御 ==========*/

    /// <summary>
    /// メニュー全体の表示/非表示 + ポーズ状態のON/OFF
    /// </summary>
    /// <param name="enable">true=表示(ポーズON) / false=非表示(ポーズOFF)</param>
    /// <param name="forceInit">Awakeなど初期化用。trueならtimeScaleを1に戻す</param>
    private void SetVisible(bool enable, bool forceInit)
    {
        visible = enable;

        // メニューのCanvasGroupをまとめてON/OFF
        cg.alpha = enable ? 1.0f : 0.0f;
        cg.interactable = enable;
        cg.blocksRaycasts = enable;

        if (enable)
        {
            // メニューを開いた瞬間
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0.0f;
            IsPaused = true;

            // 開いた瞬間の初期カーソル位置は Back
            current = MenuItem.Back;
            ApplySelectionVisuals();

            isPerformingAction = false;
        }
        else
        {
            // メニューを閉じた瞬間
            Time.timeScale = forceInit ? 1.0f : prevTimeScale;
            IsPaused = false;
            isPerformingAction = false;
        }
    }

    /*========== 視覚ハイライト ==========*/

    /// <summary>
    /// 現在選択中の項目だけ明るく、他は暗くする
    /// </summary>
    private void ApplySelectionVisuals()
    {
        ApplyCanvasGroup(retryGroup, current == MenuItem.Retry);
        ApplyCanvasGroup(exitStageGroup, current == MenuItem.ExitStage);
        ApplyCanvasGroup(backGroup, current == MenuItem.Back);
    }

    private static void ApplyCanvasGroup(CanvasGroup g, bool active)
    {
        if (!g)
        {
            return;
        }

        // 選択中: alpha=1.0
        // 非選択: alpha=0.4 など
        g.alpha = active ? 1.0f : 0f;
        g.interactable = active;
        g.blocksRaycasts = active;
    }

    /*========== 決定SEとシーン遷移 ==========*/

    private void PlaySE(AudioClip clip)
    {
        if (!clip || !seSource)
        {
            return;
        }
        seSource.PlayOneShot(clip);
    }

    private IEnumerator PlayThenLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            yield break;
        }

        isPerformingAction = true;

        // 決定音を鳴らす
        PlaySE(decideSE);

        //  ここが重要
        // 「SEが終わるまで待たない」。即座に次の処理へ進む。
        // 1. ポーズ解除
        Time.timeScale = prevTimeScale;
        IsPaused = false;

        // 2. シーン遷移（同期ロード）
        SceneManager.LoadScene(sceneName);
    }

    /*========== 外部確認用 ==========*/
    public bool GetVisible()
    {
        return visible;
    }
}
