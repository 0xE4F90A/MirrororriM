using UnityEngine;

/// <summary>
/// Esc キーで CanvasGroup 全体の表示／非表示をトグルする
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class ToggleObject : MonoBehaviour
{
    /*========== Inspector ==========*/

    [Header("表示切替キー (既定：Esc)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    /*========== 内部状態 ==========*/

    private CanvasGroup cg;   // キャッシュ
    private bool visible;     // 現在の表示状態

    /*========== Unity 標準メソッド ==========*/

    private void Awake()
    {
        // 同じ GameObject に必ず付いているはず
        cg = GetComponent<CanvasGroup>();

        // 起動時は非表示にしておく（好みに応じて変更可）
        SetVisible(false);
    }

    private void Update()
    {
        // 指定キーを押したら表示状態を反転
        if (Input.GetKeyDown(toggleKey))
        {
            SetVisible(!visible);
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
}
