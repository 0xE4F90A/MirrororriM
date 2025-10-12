using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TyperScene : MonoBehaviour
{
    [Serializable]
    private sealed class Binding
    {
        [Header("遷移先シーン指定（名前優先）")]
        [SerializeField] private bool m_UseSceneName = true;
        [SerializeField] private string m_SceneName = string.Empty;
        [SerializeField] private int m_SceneBuildIndex = -1;

        [Header("この配列のどれかが押されたら遷移")]
        [SerializeField] private KeyCode[] m_Keys = Array.Empty<KeyCode>();

        [Header("ロード方法")]
        [SerializeField] private bool m_Async = false;
        [SerializeField] private LoadSceneMode m_LoadMode = LoadSceneMode.Single;

        public bool IsTriggeredThisFrame()
        {
            var keys = m_Keys;
            for (int i = 0; i < keys.Length; ++i)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryResolveScene(out string sceneName, out int buildIndex)
        {
            sceneName = default;
            buildIndex = -1;

            if (m_UseSceneName)
            {
                if (string.IsNullOrEmpty(m_SceneName))
                {
                    return false;
                }
                if (!Application.CanStreamedLevelBeLoaded(m_SceneName))
                {
                    return false;
                }
                sceneName = m_SceneName;
                return true;
            }
            else
            {
                if (m_SceneBuildIndex < 0 || m_SceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    return false;
                }
                buildIndex = m_SceneBuildIndex;
                return true;
            }
        }

        public void Load()
        {
            if (!TryResolveScene(out string name, out int index))
            {
                Debug.LogError("[KeyToSceneLoader] シーン指定が不正です。名前/ビルドインデックスと Build Settings を確認してください。");
                return;
            }

            if (m_Async)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    SceneManager.LoadSceneAsync(name, m_LoadMode);
                }
                else
                {
                    SceneManager.LoadSceneAsync(index, m_LoadMode);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(name))
                {
                    SceneManager.LoadScene(name, m_LoadMode);
                }
                else
                {
                    SceneManager.LoadScene(index, m_LoadMode);
                }
            }
        }
    }

    [Header("キー → シーン の対応一覧（上から優先）")]
    [SerializeField] private Binding[] m_Bindings = Array.Empty<Binding>();

    private void Update()
    {
        var bindings = m_Bindings;
        for (int i = 0; i < bindings.Length; ++i)
        {
            var b = bindings[i];
            if (b == null)
            {
                continue;
            }

            if (b.IsTriggeredThisFrame())
            {
                b.Load();
                break; // 1フレームで複数遷移しないように
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 軽い検証（配列が null にならないように）
        if (m_Bindings == null)
        {
            m_Bindings = Array.Empty<Binding>();
        }
        else
        {
            for (int i = 0; i < m_Bindings.Length; ++i)
            {
                if (m_Bindings[i] == null)
                {
                    m_Bindings[i] = new Binding();
                }
            }
        }
    }
#endif
}
