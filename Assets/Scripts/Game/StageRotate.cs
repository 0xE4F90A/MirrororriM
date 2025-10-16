using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageRotate : MonoBehaviour
{
    [Header("=== 近接判定 ===")]
    [SerializeField] private Transform m_Base;          // 距離の片方
    [SerializeField] private Transform m_Goal;          // 距離の片方
    [SerializeField, Min(0f)] private float m_TriggerDistance = 0.5f;

    [Header("=== 回転対象とパラメータ（ローカル回転） ===")]
    [SerializeField] private Transform m_TargetToRotate; // 未設定なら this.transform
    public enum Axis { X, Y, Z }
    [SerializeField] private Axis m_Axis = Axis.X;
    [SerializeField] private float m_FromAngle = 0f;     // 例: 0
    [SerializeField] private float m_ToAngle = 90f;    // 例: 90
    [SerializeField, Min(0.1f)] private float m_SpeedDegPerSec = 90f;

    [Header("=== 動作オプション ===")]
    [SerializeField, Tooltip("範囲外に出たら FromAngle に戻す")]
    private bool m_AutoReverseWhenFar = true;
    [SerializeField, Tooltip("一度 ToAngle まで回したら以降は固定（AutoReverse が無効化されます）")]
    private bool m_OneShot = false;

    [Header("=== SE（任意） ===")]
    [SerializeField] private AudioSource m_AudioSource;
    [SerializeField, Tooltip("回転開始時（近接入り）")] private AudioClip m_SeStart;
    [SerializeField, Tooltip("ToAngle 到達時")] private AudioClip m_SeComplete;
    [SerializeField, Tooltip("範囲外に抜けて戻し開始")] private AudioClip m_SeReverse;

    // ランタイム
    private Quaternion m_BaseLocalRot; // 合成基準
    private float m_CmdAngle;          // 現在の指令角（From..To を往復）
    private bool m_InRangePrev;       // 前フレームの近接状態
    private bool m_CompletedOnce;     // OneShot 用

    private void Awake()
    {
        if (!m_TargetToRotate) m_TargetToRotate = transform;
        if (!m_AudioSource) m_AudioSource = GetComponent<AudioSource>();
        m_BaseLocalRot = m_TargetToRotate.localRotation;

        // 初期角を From に合わせてセット
        m_CmdAngle = m_FromAngle;
        ApplyRotation(m_CmdAngle);
    }

    private void Update()
    {
        if (!m_Base || !m_Goal || !m_TargetToRotate) return;

        // 近接判定
        float dist = Vector3.Distance(m_Base.position, m_Goal.position);
        bool inRange = dist <= m_TriggerDistance;

        // OneShot 完了後は固定
        if (m_OneShot && m_CompletedOnce)
            return;

        // 目標角
        float targetAngle;
        if (inRange)
        {
            targetAngle = m_ToAngle;
            if (inRange != m_InRangePrev) PlayOneShot(m_SeStart);
        }
        else
        {
            if (m_OneShot) return; // OneShot かつ範囲外は何もしない
            targetAngle = m_AutoReverseWhenFar ? m_FromAngle : m_CmdAngle;
            if (m_AutoReverseWhenFar && inRange != m_InRangePrev) PlayOneShot(m_SeReverse);
        }

        // 角度更新（安定の MoveTowardsAngle）
        float next = Mathf.MoveTowardsAngle(m_CmdAngle, targetAngle, m_SpeedDegPerSec * Time.deltaTime);
        if (!Mathf.Approximately(next, m_CmdAngle))
        {
            m_CmdAngle = next;
            ApplyRotation(m_CmdAngle);
        }

        // ToAngle 到達イベント
        if (inRange && Mathf.Abs(Mathf.DeltaAngle(m_CmdAngle, m_ToAngle)) <= 0.01f)
        {
            if (!m_CompletedOnce) PlayOneShot(m_SeComplete);
            m_CompletedOnce = m_OneShot || m_CompletedOnce;
        }

        m_InRangePrev = inRange;
    }

    private void ApplyRotation(float angleDeg)
    {
        Quaternion rq =
            (m_Axis == Axis.X) ? Quaternion.AngleAxis(angleDeg, Vector3.right) :
            (m_Axis == Axis.Y) ? Quaternion.AngleAxis(angleDeg, Vector3.up) :
                                 Quaternion.AngleAxis(angleDeg, Vector3.forward);

        m_TargetToRotate.localRotation = m_BaseLocalRot * rq;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (!clip) return;
        if (m_AudioSource) m_AudioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, m_TargetToRotate.position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (m_Goal == null) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(m_Goal.position, m_TriggerDistance);
        Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
        Gizmos.DrawWireSphere(m_Goal.position, m_TriggerDistance);

        if (m_Base)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(m_Base.position, m_Goal.position);
        }
    }
#endif
}
