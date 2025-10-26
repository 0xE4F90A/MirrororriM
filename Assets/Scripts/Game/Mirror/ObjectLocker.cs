using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectLocker : MonoBehaviour
{
    [Header("=== 判定ターゲット（未設定なら自身：位置/回転の一致を見る対象） ===")]
    [SerializeField] private Transform m_Target;

    [Header("=== 透明床の使用設定 ===")]
    [SerializeField, Tooltip("ONのときだけ『指定の床の上に立っている間だけロック可能』という制限を使います。OFFなら床チェックなしで常に判定します")]
    private bool m_EnableFloorCheck = true;

    [Header("=== ロックを許可する床（透明PlaneなどのCollider） ===")]
    [SerializeField, Tooltip("この床コライダ“の上に”立っている時だけロック可能（m_EnableFloorCheckがONのときのみ有効）")]
    private Collider m_FloorCollider;

    [Header("=== 床の上に立っているか判定するキャラ（Subject） ===")]
    [SerializeField, Tooltip("メインとなるチェック対象（未設定なら m_Target を使用）")]
    private Transform m_Subject;

    [SerializeField, Tooltip("追加のチェック対象。誰か一人でも床の上ならOK。'全員必須'にすると全員が床の上でないとダメ")]
    private Transform[] m_ExtraSubjects;

    [SerializeField, Tooltip("ON なら『全員がこの床に立っているときだけ』許可。OFF なら誰か一人でも乗っていれば許可")]
    private bool m_AllSubjectsMustBeOnFloor = false;

    [Header("=== 足元サンプル位置のローカルオフセット ===")]
    [SerializeField, Tooltip("Subjectのどこを“足元”として判定するか。例: (0,-0.9,0) など")]
    private Vector3 m_CheckLocalOffset = Vector3.zero;

    [Header("=== 足元から下向きの接地検出パラメータ ===")]
    [SerializeField, Tooltip("足元から少し上にずらしてキャスト開始する高さ[m]（埋まり対策）")]
    private float m_ProbeOriginOffsetY = 0.1f;

    [SerializeField, Tooltip("下向きSphereCastの半径[m]（足の太さイメージ）")]
    private float m_ProbeRadius = 0.15f;

    [SerializeField, Tooltip("下向きに飛ばす距離[m]（段差許容）")]
    private float m_ProbeDistance = 0.6f;

    [SerializeField, Tooltip("許容する床の傾斜角[deg]。0=完全水平のみ、60ならかなり斜面でもOK")]
    [Range(0f, 89f)]
    private float m_MaxGroundSlope = 50f;

    // --- 一致条件（従来どおり） ---
    [Header("=== 位置ターゲット（World） ===")]
    [SerializeField] private Vector3 m_WorldPositionTarget = Vector3.zero;
    [SerializeField] private bool m_UsePosX = false;
    [SerializeField] private bool m_UsePosY = false;
    [SerializeField] private bool m_UsePosZ = false;

    [Header("=== 回転ターゲット（World Euler, -180..180 推奨） ===")]
    [SerializeField] private Vector3 m_WorldEulerTarget = Vector3.zero; // -180..180 を想定
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
        if (!m_Target)
        {
            m_Target = transform;
        }

        if (!m_AudioSource)
        {
            m_AudioSource = GetComponent<AudioSource>();
        }

        // -180..180 に正規化しておく
        m_WorldEulerTarget = ToSignedEuler(m_WorldEulerTarget);

        // 念のためパラメータの下限を潰しておく
        m_ProbeRadius = Mathf.Max(0f, m_ProbeRadius);
        m_ProbeDistance = Mathf.Max(0f, m_ProbeDistance);
    }

    private void Update()
    {
        if (IsMatched)
        {
            return;
        }

        // ▼ オン/オフボタンがONのときだけ床チェックを強制する
        if (m_EnableFloorCheck)
        {
            // キャラクターが「指定した床コライダの上」に立っていなければ評価しない
            if (!AreSubjectsOnFloor())
            {
                return;
            }
        }

        // ▼ （床チェックOK or チェック無効）なら、位置/回転の一致チェックを実行
        bool matched = EvaluateNow();
        if (matched)
        {
            IsMatched = true;
            OnMatchedChanged?.Invoke(true);
            PlayOneShotSafe(m_SeOnLock);
        }
    }

    //========================
    // 指定した床の上に立っているか？
    //========================
    private bool AreSubjectsOnFloor()
    {
        if (!m_FloorCollider)
        {
            // 床未割り当て → 安全側でfalse
            return false;
        }

        // チェック対象を列挙（m_Subject 未設定なら m_Target を使う）
        int totalCount = 0;
        int onFloorCount = 0;

        // メイン
        Transform primary = m_Subject ? m_Subject : m_Target;
        if (primary)
        {
            totalCount++;
            if (IsOnFloor(primary))
            {
                onFloorCount++;
                if (!m_AllSubjectsMustBeOnFloor)
                {
                    return true; // 誰か一人でOKモードなら即許可
                }
            }
        }

        // 追加対象
        if (m_ExtraSubjects != null && m_ExtraSubjects.Length > 0)
        {
            for (int i = 0; i < m_ExtraSubjects.Length; i++)
            {
                Transform s = m_ExtraSubjects[i];
                if (!s)
                {
                    continue;
                }

                totalCount++;
                if (IsOnFloor(s))
                {
                    onFloorCount++;
                    if (!m_AllSubjectsMustBeOnFloor)
                    {
                        return true; // 誰か一人でOK
                    }
                }
            }
        }

        if (totalCount == 0)
        {
            // 誰もいない＝判定不可
            return false;
        }

        // 全員必須モードなら、全員が床上にいる必要あり
        return m_AllSubjectsMustBeOnFloor ? (onFloorCount == totalCount) : (onFloorCount > 0);
    }

    // 1人の Subject が「指定床の上」に立っているか？
    private bool IsOnFloor(Transform subject)
    {
        // 足元ワールド座標（Subjectのローカルからオフセット）
        Vector3 footPos = subject.TransformPoint(m_CheckLocalOffset);

        // 少し上から下にスフィアキャストして接地確認
        Vector3 castOrigin = footPos + Vector3.up * m_ProbeOriginOffsetY;
        Vector3 castDir = Vector3.down;

        if (Physics.SphereCast(
                castOrigin,
                m_ProbeRadius,
                castDir,
                out RaycastHit hit,
                m_ProbeDistance,
                ~0, // 全レイヤー対象。必要なら LayerMask に変えてOK
                QueryTriggerInteraction.Collide))
        {
            // 傾斜角（法線と上方向の角度）
            float slopeDeg = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeDeg > m_MaxGroundSlope)
            {
                return false;
            }

            // ヒットしたコライダが「この床」かどうか
            if (IsSameFloorCollider(hit.collider, m_FloorCollider))
            {
                return true;
            }
        }

        return false;
    }

    // 同じ床判定かどうか（親子関係も許容）
    private static bool IsSameFloorCollider(Collider hitCol, Collider floorCol)
    {
        if (!hitCol || !floorCol)
        {
            return false;
        }

        if (hitCol == floorCol)
        {
            return true;
        }

        // 子オブジェクトなどで分割されている場合も拾いたいので親子関係も許可
        Transform hitRoot = hitCol.transform;
        Transform floorRoot = floorCol.transform;

        if (hitRoot == floorRoot)
        {
            return true;
        }
        if (hitRoot.IsChildOf(floorRoot))
        {
            return true;
        }
        if (floorRoot.IsChildOf(hitRoot))
        {
            return true;
        }

        return false;
    }

    //========================
    // 位置/回転の一致ロジック（従来）
    //========================
    public bool EvaluateNow()
    {
        Transform t = m_Target ? m_Target : transform;

        bool any = false;
        bool ok = true;

        // --- 位置（ワールド） ---
        Vector3 p = t.position;
        if (m_UsePosX)
        {
            any = true;
            ok &= Mathf.Abs(p.x - m_WorldPositionTarget.x) <= m_PositionEpsilon;
        }
        if (m_UsePosY)
        {
            any = true;
            ok &= Mathf.Abs(p.y - m_WorldPositionTarget.y) <= m_PositionEpsilon;
        }
        if (m_UsePosZ)
        {
            any = true;
            ok &= Mathf.Abs(p.z - m_WorldPositionTarget.z) <= m_PositionEpsilon;
        }

        // --- 回転（ワールド） ---
        bool useAnyRot = m_UseRotX || m_UseRotY || m_UseRotZ;
        if (useAnyRot)
        {
            any = true;

            if (m_UseRotX && m_UseRotY && m_UseRotZ)
            {
                // 全軸ONは Quaternion で比較（オイラー角の非一意性を回避）
                Quaternion qNow = t.rotation;                               // world
                Quaternion qTarget = Quaternion.Euler(m_WorldEulerTarget);  // world
                ok &= Quaternion.Angle(qNow, qTarget) <= m_AngleEpsilon;
            }
            else
            {
                // 個別軸だけ比較。-180..180 に揃えてから比較
                Vector3 eWorld = t.eulerAngles;
                Vector3 eSigned = ToSignedEuler(eWorld);

                if (m_UseRotX)
                {
                    ok &= AngleApproximatelySigned(eSigned.x, m_WorldEulerTarget.x, m_AngleEpsilon);
                }
                if (m_UseRotY)
                {
                    ok &= AngleApproximatelySigned(eSigned.y, m_WorldEulerTarget.y, m_AngleEpsilon);
                }
                if (m_UseRotZ)
                {
                    ok &= AngleApproximatelySigned(eSigned.z, m_WorldEulerTarget.z, m_AngleEpsilon);
                }
            }
        }

        // 1つも条件がONじゃなければロックしない
        return any && ok;
    }

    // ===== 角度ユーティリティ =====
    private static float ToSigned180(float aDeg)
    {
        return Mathf.DeltaAngle(0f, aDeg);
    }

    private static Vector3 ToSignedEuler(in Vector3 eulerAny)
    {
        return new Vector3(
            ToSigned180(eulerAny.x),
            ToSigned180(eulerAny.y),
            ToSigned180(eulerAny.z)
        );
    }

    private static bool AngleApproximatelySigned(float aSignedDeg, float bSignedDeg, float epsDeg)
    {
        return Mathf.Abs(Mathf.DeltaAngle(aSignedDeg, bSignedDeg)) <= epsDeg;
    }

    // ===== オーディオ =====
    private void PlayOneShotSafe(AudioClip clip)
    {
        if (!clip)
        {
            return;
        }

        if (m_AudioSource)
        {
            m_AudioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector でいじった角度を常に -180..180 に整える
        m_WorldEulerTarget = ToSignedEuler(m_WorldEulerTarget);

        // 安全側
        m_ProbeRadius = Mathf.Max(0f, m_ProbeRadius);
        m_ProbeDistance = Mathf.Max(0f, m_ProbeDistance);
    }

    private void OnDrawGizmosSelected()
    {
        // 足元サンプル点とキャストの可視化
        Gizmos.color = Color.cyan;

        void DrawProbeFor(Transform s)
        {
            if (!s) return;

            Vector3 footPos = s.TransformPoint(m_CheckLocalOffset);
            Vector3 origin = footPos + Vector3.up * m_ProbeOriginOffsetY;
            Vector3 endPos = origin + Vector3.down * m_ProbeDistance;

            // 開始と終了の球
            Gizmos.DrawWireSphere(origin, m_ProbeRadius);
            Gizmos.DrawWireSphere(endPos, m_ProbeRadius);

            // ライン
            Gizmos.DrawLine(origin, endPos);
        }

        Transform primary = m_Subject ? m_Subject : m_Target;
        DrawProbeFor(primary);

        if (m_ExtraSubjects != null)
        {
            for (int i = 0; i < m_ExtraSubjects.Length; i++)
            {
                DrawProbeFor(m_ExtraSubjects[i]);
            }
        }

        // 床コライダの可視化は、チェックが有効なときのみ描くと分かりやすい
        if (m_EnableFloorCheck && m_FloorCollider)
        {
            if (m_FloorCollider is BoxCollider box)
            {
                Gizmos.color = Color.green;
                Transform tf = box.transform;
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(tf.position, tf.rotation, tf.lossyScale);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = prev;
            }
            else
            {
                Gizmos.color = Color.yellow;
                Bounds b = m_FloorCollider.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
#endif

    // 互換API
    public bool GetLocked()
    {
        return IsMatched;
    }

    // 再チャレンジを許可したいときに手動で解除
    public void ResetLock()
    {
        if (IsMatched)
        {
            IsMatched = false;
            OnMatchedChanged?.Invoke(false);
        }
    }
}
