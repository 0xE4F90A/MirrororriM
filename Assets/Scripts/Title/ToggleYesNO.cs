#if UNITY_EDITOR
using UnityEditor;   // 忘れずに
#endif
using UnityEngine;

public sealed class ToggleYesNo : MonoBehaviour
{
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

    /*========== Unity ==========*/
    private void Awake()
    {
        if (!yesGroup) yesGroup = transform.Find("YES")?.GetComponent<CanvasGroup>();
        if (!noGroup) noGroup = transform.Find("NO")?.GetComponent<CanvasGroup>();

        SetGroupVisible(Visible.No);        // 初期 NO 表示
    }

    private void Update()
    {
        if (Input.GetKeyDown(yesKey))
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

        if (isExit && Input.GetKeyDown(exitKey))
            QuitGame();

        if (!isExit && Input.GetKeyDown(exitKey))
            TitleState.SHOW_EXIT_MENU = false;
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
