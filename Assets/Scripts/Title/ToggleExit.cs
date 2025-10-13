#if UNITY_EDITOR
using UnityEditor;   // 忘れずに
#endif
using System.Collections;
using UnityEngine;

/// <summary>
/// Esc キーで CanvasGroup 全体の表示／非表示をトグルする
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class ToggleExit : MonoBehaviour
{
    /*========== Inspector ==========*/

    [Header("表示切替キー (既定：Esc)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    /*========== 内部状態 ==========*/

    private CanvasGroup cg;   // キャッシュ
    private bool visible;     // 現在の表示状態

    /*========== Inspector ==========*/
    [Header("YES と NO の CanvasGroup")]
    [SerializeField] private CanvasGroup yesGroup;
    [SerializeField] private CanvasGroup noGroup;

    [Header("各キー設定")]
    [SerializeField] private KeyCode yesKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode noKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode resetKey = KeyCode.Escape;
    [SerializeField] private KeyCode exitKey = KeyCode.Return;

    [Header("SE 再生設定")]
    [SerializeField] private AudioSource seSource;      // UI用AudioSource（未指定なら自動追加）
    [SerializeField] private AudioClip selectSE;        // SE1：選択移動
    [SerializeField] private AudioClip decideSE;        // SE2：決定
    [Tooltip("決定SEを鳴らし終えてから終了するか")]
    [SerializeField] private bool waitDecideSEBeforeQuit = true;

    /*========== 内部 ==========*/
    private enum Visible { None, Yes, No }
    private Visible current = Visible.No;   // 起動時 NO 表示
    private bool isExit = false;            // YES → Enter で終了フラグ

    /*========== Unity 標準メソッド ==========*/

    private void Awake()
    {
        // 同じ GameObject に必ず付いているはず
        cg = GetComponent<CanvasGroup>();

        // 起動時は非表示にしておく（好みに応じて変更可）
        SetVisible(false);

        if (!yesGroup) yesGroup = transform.Find("YES")?.GetComponent<CanvasGroup>();
        if (!noGroup) noGroup = transform.Find("NO")?.GetComponent<CanvasGroup>();

        SetGroupVisible(Visible.No);        // 初期 NO 表示

        // SE用AudioSourceの用意（未割り当てなら自動追加）
        if (!seSource)
        {
            seSource = GetComponent<AudioSource>();
            if (!seSource)
            {
                seSource = gameObject.AddComponent<AudioSource>();
            }
        }
        seSource.playOnAwake = false;
        seSource.spatialBlend = 0f; // 2D再生
    }

    private void Update()
    {
        // 指定キーを押したら表示状態を反転
        if (Input.GetKeyDown(toggleKey) || PadBool.IsStartDown())
        {
            SetVisible(!visible);
        }

        // ExitMenuが非表示なら、ここで終わり（SEも鳴らさない）
        if (!visible)
        {
            return;
        }

        // 表示中のみ選択操作を受け付け
        if (visible && Input.GetKeyDown(yesKey) || PadBool.IsLeftStickLeft())
        {
            // すでに YES なら音を鳴らさない／変更もしない
            if (current != Visible.Yes)
            {
                SetGroupVisible(Visible.Yes);
                isExit = true;
                PlaySE(selectSE); // SE1
            }
        }
        else if (visible && Input.GetKeyDown(noKey) || PadBool.IsLeftStickRight())
        {
            // すでに NO なら音を鳴らさない／変更もしない
            if (current != Visible.No)
            {
                SetGroupVisible(Visible.No);
                isExit = false;
                PlaySE(selectSE); // SE1
            }
        }
        else if (visible && Input.GetKeyDown(resetKey) || PadBool.IsStartDown())
        {
            // リセットは既定で音なし（必要なら PlaySE(selectSE) を追加）
            if (current != Visible.No)
            {
                SetGroupVisible(Visible.No);
            }
            isExit = false;
        }

        // 決定
        if (Input.GetKeyDown(exitKey) || PadBool.IsADown())
        {
            if (isExit)
            {
                // 決定：SE2 → 終了
                if (waitDecideSEBeforeQuit && decideSE)
                {
                    StartCoroutine(PlayDecideThenQuit());
                }
                else
                {
                    PlaySE(decideSE);
                    QuitGame();
                }
            }
            else
            {
                // NO選択でEnter：SE2を鳴らして閉じる
                PlaySE(decideSE);
                SetVisible(false);
            }
        }
    }

    /*========== ユーティリティ ==========*/

    /// <summary>
    /// CanvasGroup の表示／非表示を一括で設定
    /// </summary>
    private void SetVisible(bool enable)
    {
        visible = enable;

        // アルファを 0 ⇆ 1 に切り替え
        cg.alpha = enable ? 1f : 0f;

        // UI への入力を受け付けるかどうか
        cg.interactable = enable;
        cg.blocksRaycasts = enable;
    }

    /*========== 共通 ==========*/
    private void SetGroupVisible(Visible next)
    {
        current = next;
        Apply(yesGroup, next == Visible.Yes);
        Apply(noGroup, next == Visible.No);
    }

    private static void Apply(CanvasGroup cg, bool enable)
    {
        if (!cg) return;
        cg.alpha = enable ? 1f : 0f;
        cg.interactable = enable;
        cg.blocksRaycasts = enable;
    }

    private void PlaySE(AudioClip clip)
    {
        if (!clip || !seSource) return;
        seSource.PlayOneShot(clip);
    }

    private IEnumerator PlayDecideThenQuit()
    {
        PlaySE(decideSE);
        float dur = decideSE ? decideSE.length / Mathf.Max(0.0001f, seSource.pitch) : 0f;
        yield return new WaitForSecondsRealtime(dur);
        QuitGame();
    }

    public bool GetVisible()
    {
        return visible;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        // すべてのバージョンで確実に Play モードを止める
        EditorApplication.isPlaying = false;
#else
        Application.Quit();                 // ビルド版を終了
#endif
    }
}
