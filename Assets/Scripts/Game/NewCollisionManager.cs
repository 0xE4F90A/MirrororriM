using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NewCollisionManager : MonoBehaviour
{
    //========================
    // ON/OFF 対象
    //========================
    [Serializable]
    public sealed class ToggleGroup
    {
        [Header("対象の Collider 群（空でも可）")]
        public Collider[] Colliders;

        [Header("対象の GameObject 群（空でも可）")]
        public GameObject[] Objects;

        public void SetEnabled(bool enabled)
        {
            if (Colliders != null)
            {
                for (int i = 0; i < Colliders.Length; ++i)
                {
                    if (Colliders[i]) Colliders[i].enabled = enabled;
                }
            }

            if (Objects != null)
            {
                for (int i = 0; i < Objects.Length; ++i)
                {
                    if (Objects[i]) Objects[i].SetActive(enabled);
                }
            }
        }
    }

    //========================
    // 1エントリ = 1つの ObjectClampMover を監視
    //========================
    [Serializable]
    public sealed class LockToggleEntry
    {
        [Header("達成判定ソース（ObjectClampMover を割当て）")]
        public ObjectClampMover Source;

        [Tooltip("true=達成(IsLocked)を反転して扱う")]
        public bool Invert;

        [Header("達成(true)のとき：ON / OFF")]
        [Tooltip("達成したら有効化する対象群")]
        public ToggleGroup OnWhenTrue;
        [Tooltip("達成したら無効化する対象群")]
        public ToggleGroup OffWhenTrue;

        [NonSerialized] public bool m_Initialized;
        [NonSerialized] public bool m_Last;
    }

    //========================
    // インスペクター
    //========================
    [Header("監視エントリ（いくつでも）")]
    [SerializeField] private LockToggleEntry[] m_Entries;

    [Header("起動時に即同期する")]
    [SerializeField] private bool m_ApplyOnEnable = true;

    [Header("デバッグログを出す")]
    [SerializeField] private bool m_DebugLog = false;

    //========================
    // ライフサイクル
    //========================
    private void OnEnable()
    {
        if (m_ApplyOnEnable)
        {
            RefreshAll(applyAlways: true);
        }
    }

    private void Update()
    {
        RefreshAll(applyAlways: false);
    }

    //========================
    // 公開API（手動反映したいときに）
    //========================
    public void ManualRefresh()
    {
        RefreshAll(applyAlways: true);
    }

    //========================
    // 内部：一括更新
    //========================
    private void RefreshAll(bool applyAlways)
    {
        if (m_Entries == null) return;

        for (int i = 0; i < m_Entries.Length; ++i)
        {
            var e = m_Entries[i];
            if (e == null) continue;

            bool state = GetCurrentState(e);   // 達成判定（反転込み）

            if (applyAlways || !e.m_Initialized || state != e.m_Last)
            {
                ApplyEntry(e, state);
                e.m_Last = state;
                e.m_Initialized = true;

                if (m_DebugLog)
                {
                    Debug.Log($"[CollisionManager] Entry#{i} -> {(state ? "LOCKED(TRUE)" : "UNLOCK(FALSE)")}");
                }
            }
        }
    }

    private static bool GetCurrentState(LockToggleEntry e)
    {
        bool s = false;
        if (e.Source != null)
        {
            s = e.Source.IsLocked; // ObjectClampMover 側の達成フラグ
        }
        return e.Invert ? !s : s;
    }

    private static void ApplyEntry(LockToggleEntry e, bool state)
    {
        // 達成(true)：OnWhenTrue を ON、OffWhenTrue を OFF
        // 未達(false)：逆適用
        if (state)
        {
            e.OnWhenTrue?.SetEnabled(true);
            e.OffWhenTrue?.SetEnabled(false);
        }
        else
        {
            e.OnWhenTrue?.SetEnabled(false);
            e.OffWhenTrue?.SetEnabled(true);
        }
    }
}
