#if UNITY_EDITOR
using UnityEditor;   // 忘れずに
#endif
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
    }

    private void Update()
    {
        // 指定キーを押したら表示状態を反転
        if (Input.GetKeyDown(toggleKey))
            SetVisible(!visible);

        if (visible && Input.GetKeyDown(yesKey))
        {
            SetGroupVisible(Visible.Yes);
            isExit = true;
        }
        else if (Input.GetKeyDown(noKey))
        {
            SetGroupVisible(Visible.No);
            isExit = false;
        }
        else if (Input.GetKeyDown(resetKey))
        {
            SetGroupVisible(Visible.No);
            isExit = false;
        }

        if (Input.GetKeyDown(exitKey))
        {
            if (isExit) QuitGame();
            else SetVisible(false);
        }
    }

    /*========== ユーティリティ ==========*/

    /// <summary>
    /// CanvasGroup の表示／非表示を一括で設定
    /// </summary>
    /// <param name="enable">true: 表示 / false: 非表示</param>
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

