using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectLocker : MonoBehaviour
{
    [Header("=== 判定ターゲット（未設定なら自身） ===")]
    [SerializeField] private Transform m_Target;

    // --- 位置（ワールド） ---
    [Header("=== 位置ターゲット（World） ===")]
    [SerializeField] private Vector3 m_WorldPositionTarget = Vector3.zero;
    [SerializeField] private bool m_UsePosX = false;
    [SerializeField] private bool m_UsePosY = false;
    [SerializeField] private bool m_UsePosZ = false;

    // --- 回転（ワールドEuler） ---
    [Header("=== 回転ターゲット（World Euler, -180..180 推奨） ===")]
    [SerializeField] private Vector3 m_WorldEulerTarget = Vector3.zero; // ← -180..180 を想定
    [SerializeField] private bool m_UseRotX = false;
    [SerializeField] private bool m_UseRotY = false;
    [SerializeField] private bool m_UseRotZ = false;

    [Header("=== 許容誤差 ===")]
    [SerializeField, Tooltip("位置の一致とみなす誤差[m]")] private float m_PositionEpsilon = 0.01f;
    [SerializeField, Tooltip("角度の一致とみなす誤差[deg]")] private float m_AngleEpsilon = 0.5f;

    [Header("=== 一致時 SE（=SE1） ===")]
    [SerializeField, Tooltip("一致(ロック)した瞬間に再生する SE1")] private AudioClip m_SeOnLock;
    [SerializeField] private AudioSource m_AudioSource;

    public bool IsMatched { get; private set; }
    public event Action<bool> OnMatchedChanged;

    private void Awake()
    {
        if (!m_Target) m_Target = transform;
        if (!m_AudioSource) m_AudioSource = GetComponent<AudioSource>();
        // 入力されたターゲット角を -180..180 に正規化
        m_WorldEulerTarget = ToSignedEuler(m_WorldEulerTarget);
    }

    private void Update()
    {
        if (IsMatched) return;

        bool matched = EvaluateNow();
        if (matched)
        {
            IsMatched = true;
            OnMatchedChanged?.Invoke(true);
            PlayOneShotSafe(m_SeOnLock); // 一致時にSE1再生
        }
    }

    public bool EvaluateNow()
    {
        Transform t = m_Target ? m_Target : transform;

        bool any = false; // 何か1つでも条件が有効か
        bool ok = true;  // 有効条件がすべて満たされたか

        // --- 位置（ワールド） ---
        Vector3 p = t.position;
        if (m_UsePosX) { any = true; ok &= Mathf.Abs(p.x - m_WorldPositionTarget.x) <= m_PositionEpsilon; }
        if (m_UsePosY) { any = true; ok &= Mathf.Abs(p.y - m_WorldPositionTarget.y) <= m_PositionEpsilon; }
        if (m_UsePosZ) { any = true; ok &= Mathf.Abs(p.z - m_WorldPositionTarget.z) <= m_PositionEpsilon; }

        // --- 回転（ワールド） ---
        bool useAnyRot = m_UseRotX || m_UseRotY || m_UseRotZ;
        if (useAnyRot)
        {
            any = true;

            if (m_UseRotX && m_UseRotY && m_UseRotZ)
            {
                // 全軸ONは Quaternion で向きそのものを比較（Euler非一意性を回避）
                Quaternion qNow = t.rotation;                           // world
                Quaternion qTarget = Quaternion.Euler(m_WorldEulerTarget);  // world
                ok &= Quaternion.Angle(qNow, qTarget) <= m_AngleEpsilon;
            }
            else
            {
                // 個別軸比較：現在角を -180..180 に変換してから比較（視覚と一致）
                Vector3 eWorld = t.eulerAngles;
                Vector3 eSigned = ToSignedEuler(eWorld); // -180..180

                if (m_UseRotX) ok &= AngleApproximatelySigned(eSigned.x, m_WorldEulerTarget.x, m_AngleEpsilon);
                if (m_UseRotY) ok &= AngleApproximatelySigned(eSigned.y, m_WorldEulerTarget.y, m_AngleEpsilon);
                if (m_UseRotZ) ok &= AngleApproximatelySigned(eSigned.z, m_WorldEulerTarget.z, m_AngleEpsilon);
            }
        }

        // 有効な条件が1つも無ければ false、有効条件全部OKなら true
        return any && ok;
    }

    // ===== 角度ユーティリティ =====

    // 任意角を -180..180 に正規化
    private static float ToSigned180(float aDeg) => Mathf.DeltaAngle(0f, aDeg);

    // Vector3(Euler) を -180..180 に正規化
    private static Vector3 ToSignedEuler(in Vector3 eulerAny)
        => new Vector3(ToSigned180(eulerAny.x), ToSigned180(eulerAny.y), ToSigned180(eulerAny.z));

    // -180..180 同士の比較（差分は DeltaAngle が最も堅い）
    private static bool AngleApproximatelySigned(float aSignedDeg, float bSignedDeg, float epsDeg)
        => Mathf.Abs(Mathf.DeltaAngle(aSignedDeg, bSignedDeg)) <= epsDeg;

    // ===== オーディオ =====
    private void PlayOneShotSafe(AudioClip clip)
    {
        if (!clip) return;
        if (m_AudioSource) m_AudioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // インスペクタ編集時も -180..180 に整形（視覚と一致）
        m_WorldEulerTarget = ToSignedEuler(m_WorldEulerTarget);
    }
#endif

    // 互換用（既存呼び出しがある場合のため）
    public bool GetLocked() => IsMatched;
}
