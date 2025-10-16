using UnityEngine;

[DisallowMultipleComponent]
public sealed class BgmPlayer : MonoBehaviour
{
    [Header("このシーンで流したいBGM")]
    [SerializeField] private AudioClip m_BgmClip;

    [Header("シーン入場時に即適用（同曲なら継続のままパラメータのみ反映）")]
    [SerializeField] private bool m_PlayOnSceneEnter = true;

    [Header("フェード時間(秒・負値でマネージャ既定)")]
    [SerializeField] private float m_FadeSeconds = -1f;

    [Header("曲ボリューム(0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float m_TrackVolume = 1f;

    [Header("通常ループ")]
    [SerializeField] private bool m_Loop = true;

    [Header("区間ループを使う")]
    [SerializeField] private bool m_UseLoopRegion = false;

    [Header("区間ループ開始(秒)")]
    [SerializeField] private float m_LoopStartSec = 0f;

    [Header("区間ループ終了(秒・0以下で自動=曲末)")]
    [SerializeField] private float m_LoopEndSec = 0f;

    [Header("開始位置(秒)")]
    [SerializeField] private float m_StartTimeSec = 0f;

    [Header("ピッチ(再生速度)")]
    [SerializeField] private float m_Pitch = 1f;

    [Header("同じ曲が再生中でもフェードでリスタートする")]
    [SerializeField] private bool m_ForceRestartIfSame = false;

    [Header("同じ曲が再生中なら開始位置だけ適用（頭出し）")]
    [SerializeField] private bool m_ApplyStartTimeWhenSame = false;

    private void Start()
    {
        if (!m_PlayOnSceneEnter) return;

        var mgr = FindFirstObjectByType<BgmManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[BgmPlayer] BgmManager が見つかりません。");
            return;
        }

        float? fadeOpt = (m_FadeSeconds >= 0f) ? m_FadeSeconds : (float?)null;

        mgr.PlayBgm(
            m_BgmClip,
            volume: m_TrackVolume,
            loop: m_Loop,
            startTimeSec: m_StartTimeSec,
            pitch: m_Pitch,
            fadeSeconds: fadeOpt,
            loopRegion: m_UseLoopRegion,
            loopStartSec: m_LoopStartSec,
            loopEndSec: m_LoopEndSec,
            forceRestartIfSame: m_ForceRestartIfSame,       // ← 追加
            applyStartTimeWhenSame: m_ApplyStartTimeWhenSame    // ← 追加
        );
    }
}
