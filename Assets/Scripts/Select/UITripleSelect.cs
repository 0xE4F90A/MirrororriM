using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UITripleSelect : MonoBehaviour
{
    [Header("並べた3つのRawImage（左→中央→右）")]
    [SerializeField] private RawImage[] m_Items = new RawImage[3];

    [Header("フォーカス色設定")]
    [SerializeField] private Color m_FocusedColor = Color.white;                 // #FFFFFF
    [SerializeField] private Color m_UnfocusedColor = new Color(0.266f, 0.266f, 0.266f, 1f); // #444444

    [Header("入力キー設定")]
    [SerializeField] private KeyCode m_LeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode m_RightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode m_DecideKey = KeyCode.Return;
    [SerializeField] private bool m_AcceptKeypadEnter = true; // テンキーEnterも許可

    [Header("SE再生設定")]
    [SerializeField] private AudioSource m_AudioSource; // 未指定なら自動追加（2D）
    [SerializeField] private AudioClip m_SelectSE;      // SE1：フォーカス移動時
    [SerializeField] private AudioClip m_DecideSE;      // SE2：決定時
    [SerializeField] private bool m_WaitDecideSEBeforeLoad = false; // 決定SE再生後にシーン遷移

    [Header("遷移先シーン（左・中央・右）")]
    [SerializeField] private string m_SceneLeft = "GameScene";
    [SerializeField] private string m_SceneCenter = "GameScene2";
    [SerializeField] private string m_SceneRight = "GameScene3";

    private int m_CurrentIndex = 0; // 0:左, 1:中央, 2:右

    private void Awake()
    {
        // AudioSource 用意
        if (m_AudioSource == null)
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        m_AudioSource.playOnAwake = false;
        m_AudioSource.spatialBlend = 0f; // 2D

        // 最低限チェック
        if (m_Items == null || m_Items.Length != 3)
        {
            Debug.LogError("UITripleSelector: RawImage は左→中央→右の順で3つ割り当ててください。");
        }

        ApplyColors();
    }

    private void Update()
    {
        // 左右移動（端では無視：SE1鳴らさない）
        if (Input.GetKeyDown(m_LeftKey))
        {
            SetIndex(m_CurrentIndex - 1); // 端なら変化なし→SE再生なし
        }
        else if (Input.GetKeyDown(m_RightKey))
        {
            SetIndex(m_CurrentIndex + 1);
        }

        // 決定
        if (Input.GetKeyDown(m_DecideKey) || (m_AcceptKeypadEnter && Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OnDecide();
        }
    }

    private void SetIndex(int newIndex)
    {
        int clamped = Mathf.Clamp(newIndex, 0, 2);
        if (clamped == m_CurrentIndex)
        {
            // 端でさらに同方向入力 → 変化なし、SE1鳴らさない
            return;
        }

        m_CurrentIndex = clamped;
        ApplyColors();
        PlaySE(m_SelectSE); // SE1
    }

    private void ApplyColors()
    {
        for (int i = 0; i < 3; ++i)
        {
            var img = m_Items != null && i < m_Items.Length ? m_Items[i] : null;
            if (img == null) continue;

            // フォーカスのみ #FFFFFF、他は #444444
            img.color = (i == m_CurrentIndex) ? m_FocusedColor : m_UnfocusedColor;
        }
    }

    private void OnDecide()
    {
        // 遷移先決定
        string scene =
            (m_CurrentIndex == 0) ? m_SceneLeft :
            (m_CurrentIndex == 1) ? m_SceneCenter :
            m_SceneRight;

        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogWarning("UITripleSelector: 遷移先シーン名が未設定です。");
            return;
        }

        if (m_WaitDecideSEBeforeLoad && m_DecideSE != null)
        {
            StartCoroutine(Co_PlayDecideThenLoad(scene));
        }
        else
        {
            PlaySE(m_DecideSE); // SE2（即遷移だと途中で切れる場合あり）
            SceneManager.LoadScene(scene);
        }
    }

    private IEnumerator Co_PlayDecideThenLoad(string sceneName)
    {
        PlaySE(m_DecideSE);
        float dur = (m_DecideSE != null) ? m_DecideSE.length / Mathf.Max(0.0001f, m_AudioSource != null ? m_AudioSource.pitch : 1f) : 0f;
        yield return new WaitForSecondsRealtime(dur);
        SceneManager.LoadScene(sceneName);
    }

    private void PlaySE(AudioClip clip)
    {
        if (clip == null || m_AudioSource == null)
        {
            return;
        }
        m_AudioSource.PlayOneShot(clip);
    }

    // 便利：外部から初期位置を指定したい場合に呼べる
    public void SetFocusLeft()
    {
        m_CurrentIndex = 0;
        ApplyColors();
    }

    public void SetFocusCenter()
    {
        m_CurrentIndex = 1;
        ApplyColors();
    }

    public void SetFocusRight()
    {
        m_CurrentIndex = 2;
        ApplyColors();
    }
}
