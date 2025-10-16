using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public sealed class BgmManager : MonoBehaviour
{
    [Header("デフォルトのフェード時間(秒)")]
    [SerializeField] private float m_FadeSeconds = 0.75f;

    [Header("マスターボリューム(0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float m_MasterVolume = 1f;

    [Header("BGMカテゴリボリューム(0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float m_BgmBusVolume = 1f;

    [Header("AudioMixer 出力先 (任意)")]
    [SerializeField] private AudioMixerGroup m_OutputMixerGroup;

    private AudioSource m_Active;
    private AudioSource m_Backup;

    // 現在の“曲ごとの”ボリューム係数（0-1）。フェードやPlay時のvolume引数で変化。
    private float m_ActiveTrackVolume = 1f;

    // 区間ループ設定
    private bool m_UseLoopRegion;
    private float m_LoopStartSec;
    private float m_LoopEndSec;
    private Coroutine m_LoopRegionWatcher;

    private void Awake()
    {
        var all = FindObjectsByType<BgmManager>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        m_Active = gameObject.AddComponent<AudioSource>();
        m_Backup = gameObject.AddComponent<AudioSource>();
        Configure(m_Active);
        Configure(m_Backup);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private bool EnsureReady()
    {
        // 既に他の要因で破棄/未初期化の可能性に備え、ここで AudioSource を用意
        if (m_Active == null) m_Active = gameObject.GetComponent<AudioSource>();
        if (m_Backup == null)
        {
            // 1つも付いていなければ2つ追加、1つだけならもう1つ追加
            var sources = GetComponents<AudioSource>();
            if (sources == null || sources.Length == 0)
            {
                m_Active = gameObject.AddComponent<AudioSource>();
                m_Backup = gameObject.AddComponent<AudioSource>();
                Configure(m_Active);
                Configure(m_Backup);
            }
            else if (sources.Length == 1)
            {
                m_Active = sources[0];
                m_Backup = gameObject.AddComponent<AudioSource>();
                Configure(m_Backup);
                Configure(m_Active); // 念のため再適用
            }
            else
            {
                // 2つ以上あれば先頭2つを使用
                m_Active = sources[0];
                m_Backup = sources[1];
                Configure(m_Active);
                Configure(m_Backup);
            }
        }
        return (m_Active != null && m_Backup != null);
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // 必要ならシーンごとの調整を書く
    }

    private void Configure(AudioSource src)
    {
        src.playOnAwake = false;
        src.loop = true;                 // 通常はループON（区間ループ時は自前で巻き戻す）
        src.spatialBlend = 0f;           // 2D
        src.ignoreListenerPause = false;
        if (m_OutputMixerGroup != null)
        {
            src.outputAudioMixerGroup = m_OutputMixerGroup;
        }
    }

    //========================
    // 公開API
    //========================

    /// <summary>
    /// BGMを再生（同じクリップなら何もしない＝継続）。
    /// volume: 曲ごとのボリューム係数(0-1)
    /// loop: 通常ループ有無（true推奨、区間ループ時は内部で制御）
    /// startTimeSec: 再生開始位置（秒）
    /// pitch: ピッチ倍率(1=等倍)
    /// fadeSeconds: フェード時間（nullで既定）
    /// loopRegion: 区間ループを使う場合 true
    /// loopStartSec/loopEndSec: 区間ループの開始/終了（秒）
    /// </summary>
    public void PlayBgm(
    AudioClip clip,
    float volume = 1f,
    bool loop = true,
    float startTimeSec = 0f,
    float pitch = 1f,
    float? fadeSeconds = null,
    bool loopRegion = false,
    float loopStartSec = 0f,
    float loopEndSec = 0f,
    bool forceRestartIfSame = false,          // ← 追加
    bool applyStartTimeWhenSame = false       // ← 追加
)
    {
        if (!EnsureReady()) return; // 前回の怠惰初期化ヘルパー

        if (clip == null)
        {
            StopBgm(fadeSeconds);
            return;
        }

        float fade = Mathf.Max(0f, fadeSeconds.HasValue ? fadeSeconds.Value : m_FadeSeconds);

        // ★同一クリップ再生中の扱い
        if (m_Active.clip == clip && m_Active.isPlaying && !forceRestartIfSame)
        {
            // 継続しつつパラメータだけ反映
            m_ActiveTrackVolume = Mathf.Clamp01(volume);
            m_Active.loop = loop;
            m_Active.pitch = Mathf.Max(0.001f, pitch);
            if (applyStartTimeWhenSame)
            {
                m_Active.time = Mathf.Clamp(startTimeSec, 0f, clip.length);
            }
            ApplyCompositeVolume();
            SetupLoopRegion(loopRegion, loopStartSec, loopEndSec);
            return;
        }

        // ここに来たら「別曲」または「同曲でも強制リスタート」
        SwapActiveAndBackup();

        m_Active.clip = clip;
        m_Active.loop = loop;
        m_Active.pitch = Mathf.Max(0.001f, pitch);
        m_Active.time = Mathf.Clamp(startTimeSec, 0f, clip.length);
        m_ActiveTrackVolume = Mathf.Clamp01(volume);

        SetupLoopRegion(loopRegion, loopStartSec, loopEndSec);

        m_Active.volume = 0f;
        m_Active.Play();

        if (fade <= 0f)
        {
            if (m_Backup != null)
            {
                m_Backup.Stop();
                m_Backup.clip = null;
            }
            ApplyCompositeVolume(target: m_Active, linear01: 1f);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeRoutine(m_Backup, m_Active, fade));
    }


    /// <summary>
    /// 停止（フェードあり/なし）
    /// </summary>
    public void StopBgm(float? fadeSeconds = null)
    {
        if (!EnsureReady()) return;

        float fade = Mathf.Max(0f, fadeSeconds.HasValue ? fadeSeconds.Value : m_FadeSeconds);
        if (!m_Active.isPlaying)
        {
            return;
        }

        StopLoopRegionWatcher();

        if (fade <= 0f)
        {
            m_Active.Stop();
            m_Active.clip = null;
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeOutThenStop(m_Active, fade));
    }

    /// <summary> 一時停止 </summary>
    public void PauseBgm()
    {
        m_Active.Pause();
    }

    /// <summary> 再開 </summary>
    public void ResumeBgm()
    {
        m_Active.UnPause();
    }

    /// <summary> 現在のクリップ（null可） </summary>
    public AudioClip GetCurrentClip()
    {
        return m_Active != null ? m_Active.clip : null;
    }

    /// <summary> 再生位置(秒) </summary>
    public float GetCurrentTime()
    {
        if (m_Active != null && m_Active.isPlaying && m_Active.clip != null)
        {
            return m_Active.time;
        }
        return 0f;
    }

    /// <summary> 再生位置(秒)を設定 </summary>
    public void SetCurrentTime(float timeSec)
    {
        if (m_Active != null && m_Active.clip != null)
        {
            m_Active.time = Mathf.Clamp(timeSec, 0f, m_Active.clip.length);
        }
    }

    /// <summary> マスターボリューム(0-1) </summary>
    public void SetMasterVolume(float v)
    {
        m_MasterVolume = Mathf.Clamp01(v);
        ApplyCompositeVolume();
    }

    /// <summary> BGMカテゴリのボリューム(0-1) </summary>
    public void SetBgmBusVolume(float v)
    {
        m_BgmBusVolume = Mathf.Clamp01(v);
        ApplyCompositeVolume();
    }

    /// <summary> 現在曲のボリューム(0-1)を変更（フェード任意） </summary>
    public void SetTrackVolume(float v, float fadeSeconds = 0f)
    {
        v = Mathf.Clamp01(v);
        if (fadeSeconds <= 0f)
        {
            m_ActiveTrackVolume = v;
            ApplyCompositeVolume();
            return;
        }
        StopAllCoroutines();
        StartCoroutine(TrackVolumeFade(v, fadeSeconds));
    }

    /// <summary> ピッチ（再生速度） </summary>
    public void SetPitch(float pitch)
    {
        m_Active.pitch = Mathf.Max(0.001f, pitch);
    }

    /// <summary> 通常ループON/OFF（区間ループ時は内部監視が優先） </summary>
    public void SetLoop(bool loop)
    {
        m_Active.loop = loop;
    }

    /// <summary> 区間ループ設定（使わない場合はUse=false） </summary>
    public void SetLoopRegion(bool use, float loopStartSec = 0f, float loopEndSec = 0f)
    {
        SetupLoopRegion(use, loopStartSec, loopEndSec);
    }

    //========================
    // 内部処理
    //========================

    private void SwapActiveAndBackup()
    {
        var tmp = m_Backup;
        m_Backup = m_Active;
        m_Active = tmp;
    }

    private IEnumerator FadeRoutine(AudioSource from, AudioSource to, float duration)
    {
        float elapsed = 0f;
        float fromStart = (from != null) ? from.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (from != null)
            {
                from.volume = Mathf.Lerp(fromStart, 0f, t);
            }
            ApplyCompositeVolume(target: to, linear01: t); // 0→1 で上げる

            yield return null;
        }

        if (from != null)
        {
            from.Stop();
            from.clip = null;
            from.volume = 1f;
        }
        ApplyCompositeVolume(target: to, linear01: 1f);
    }

    private IEnumerator FadeOutThenStop(AudioSource src, float duration)
    {
        float elapsed = 0f;
        float start = src.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            src.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        src.Stop();
        src.clip = null;
        src.volume = 1f;
    }

    private void ApplyCompositeVolume(AudioSource target = null, float? linear01 = null)
    {
        // 合成: Master * BGMバス * 曲ボリューム * フェード線形係数
        float fade = linear01.HasValue ? Mathf.Clamp01(linear01.Value) : 1f;
        float vol = Mathf.Clamp01(m_MasterVolume * m_BgmBusVolume * m_ActiveTrackVolume * fade);

        if (target != null)
        {
            target.volume = vol;
        }
        else if (m_Active != null)
        {
            m_Active.volume = vol;
        }
    }

    private IEnumerator TrackVolumeFade(float dst, float duration)
    {
        float src = m_ActiveTrackVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            m_ActiveTrackVolume = Mathf.Lerp(src, dst, t);
            ApplyCompositeVolume();
            yield return null;
        }

        m_ActiveTrackVolume = dst;
        ApplyCompositeVolume();
    }

    private void SetupLoopRegion(bool use, float startSec, float endSec)
    {
        StopLoopRegionWatcher();

        m_UseLoopRegion = use;

        if (!use || m_Active.clip == null)
        {
            // 通常ループに戻す
            if (m_Active != null) m_Active.loop = true;
            return;
        }

        // 正規化
        float len = m_Active.clip.length;
        m_LoopStartSec = Mathf.Clamp(startSec, 0f, Mathf.Max(0f, len - 0.001f));
        m_LoopEndSec = Mathf.Clamp(endSec <= 0f ? len : endSec, m_LoopStartSec + 0.001f, len);

        // 通常ループは無効化して自前で巻き戻す
        m_Active.loop = false;
        m_LoopRegionWatcher = StartCoroutine(LoopRegionWatch());
    }

    private IEnumerator LoopRegionWatch()
    {
        while (m_UseLoopRegion && m_Active != null && m_Active.clip != null && m_Active.isPlaying)
        {
            // ループ終端到達でループ開始へ巻き戻し
            if (m_Active.time >= m_LoopEndSec)
            {
                // “クリック”を抑えるため少し前に丸め込む
                m_Active.time = m_LoopStartSec;
            }
            yield return null;
        }
        m_LoopRegionWatcher = null;
    }

    private void StopLoopRegionWatcher()
    {
        if (m_LoopRegionWatcher != null)
        {
            StopCoroutine(m_LoopRegionWatcher);
            m_LoopRegionWatcher = null;
        }
        m_UseLoopRegion = false;
    }
}
