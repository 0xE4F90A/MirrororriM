using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectClampMover : MonoBehaviour
{
    //========================
    // 定義
    //========================
    public enum Axis
    {
        X,
        Y,
        Z
    }

    [Serializable]
    public struct AxisClamp
    {
        [Tooltip("この軸のクランプを有効化")]
        public bool Enabled;

        [Tooltip("最小値（ワールド座標）")]
        public float Min;

        [Tooltip("最大値（ワールド座標）")]
        public float Max;
    }

    [Serializable]
    public struct AxisGoalRange
    {
        [Tooltip("この軸の“完了範囲”判定を有効化")]
        public bool Enabled;

        [Tooltip("下限（ワールド座標）")]
        public float Min;

        [Tooltip("上限（ワールド座標）")]
        public float Max;
    }

    [Serializable]
    public struct AngleGoalRange
    {
        [Tooltip("この軸の“完了角度範囲”判定を有効化（※ローカル角で判定）")]
        public bool Enabled;

        [Tooltip("角度下限（度）。-可。0..360を想定（wrap対応）")]
        public float Min;

        [Tooltip("角度上限（度）。-可。0..360を想定（wrap対応）")]
        public float Max;
    }

    [Serializable]
    public struct MoveKey
    {
        [Tooltip("移動に使うキー（GetKeyDown：長押し無効）")]
        public KeyCode Key;

        [Tooltip("ワールド方向（例：右へなら (1,0,0)）")]
        public Vector3 WorldDirection;

        [Tooltip("ワープ距離（例：40）")]
        public float Step;
    }

    //=== 2系統に固定した回転設定（ローカル回転） ============================
    [Serializable]
    public sealed class FixedRotation
    {
        [Header("入力")]
        [Tooltip("このキーを押したら回転します（GetKeyDown）")]
        public KeyCode Key = KeyCode.None;

        [Header("回転設定（ローカル）")]
        [Tooltip("ローカルのどの軸を回転させるか")]
        public Axis Axis = Axis.Y;

        [Tooltip("角度パターン（度）。例：0,45,90 → ピンポン 0→45→90→45→0…")]
        public float[] Angles = new float[] { 0f, 90f };

        [Header("サウンド")]
        [Tooltip("押下時に再生するSE")]
        public AudioClip Se;

        // 初回押下の扱い
        [Header("初回押下の挙動")]
        [Tooltip("初回は必ず配列の先頭(A[0])から始めます")]
        public bool StartFromFirst = true;
        [Tooltip("初回に A[0] が現在角とほぼ同じなら、A[1] から始めます")]
        public bool SkipFirstIfSame = true;

        [Tooltip("角度比較の許容誤差(度)")]
        public float FirstPressEps = 0.1f;

        // ランタイム（ピンポン用）
        [NonSerialized] public int _index = 0;
        [NonSerialized] public int _direction = +1;
        //  初回押下かどうか
        [NonSerialized] public bool _appliedOnce = false;

        public void ResetRuntime()
        {
            _index = 0;
            _direction = +1;
            _appliedOnce = false;
        }
    }
    //=======================================================================

    //========================
    // インスペクター：移動・クランプ
    //========================
    [Header("=== ワープ（ワールド座標・GetKeyDownのみ） ===")]
    [SerializeField] private MoveKey m_MoveRight = new MoveKey { Key = KeyCode.RightArrow, WorldDirection = new Vector3(1f, 0f, 0f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveLeft = new MoveKey { Key = KeyCode.LeftArrow, WorldDirection = new Vector3(-1f, 0f, 0f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveUp = new MoveKey { Key = KeyCode.UpArrow, WorldDirection = new Vector3(0f, 0f, 1f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveDown = new MoveKey { Key = KeyCode.DownArrow, WorldDirection = new Vector3(0f, 0f, -1f), Step = 1.25f };

    [Header("移動SE（SE1）")]
    [SerializeField, Tooltip("移動時に再生するSE")] private AudioClip m_SeMove;
    [SerializeField, Tooltip("SE再生に使うAudioSource（必ず割り当て推奨）")] private AudioSource m_AudioSource;

    [Header("=== ワールド座標クランプ ===")]
    [SerializeField] private AxisClamp m_ClampX = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampY = new AxisClamp { Enabled = false, Min = 0f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampZ = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };

    //========================
    // インスペクター：回転（固定2系統・ローカル）
    //========================
    [Header("=== 回転（固定2系統：A / B、ローカル） ===")]
    [SerializeField]
    private FixedRotation m_RotationA = new FixedRotation
    {
        Key = KeyCode.Y,
        Axis = Axis.Y,
        Angles = new float[] { 0f, 90f }
    };

    [SerializeField]
    private FixedRotation m_RotationB = new FixedRotation
    {
        Key = KeyCode.U,
        Axis = Axis.Z,
        Angles = new float[] { 0f, 90f }
    };

    //========================
    // インスペクター：完了ロック条件（位置＆回転“範囲”）
    //========================
    [Header("=== 完了ロック条件（位置“範囲”/ワールド） ===")]
    [SerializeField] private AxisGoalRange m_GoalPosXRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AxisGoalRange m_GoalPosYRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AxisGoalRange m_GoalPosZRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };

    [Header("=== 完了ロック条件（回転“範囲”/ローカル角） ===")]
    [SerializeField, Tooltip("回転完了範囲の評価対象（未指定なら自身）")]
    private Transform m_RotationGoalTarget;

    [SerializeField] private AngleGoalRange m_GoalRotXRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AngleGoalRange m_GoalRotYRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AngleGoalRange m_GoalRotZRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };

    [Header("許容誤差（範囲端のマージン）")]
    [SerializeField, Tooltip("位置範囲の端に対するマージン（m）")] private float m_PositionEpsilon = 0.01f;
    [SerializeField, Tooltip("角度範囲の端に対するマージン（度）")] private float m_AngleEpsilon = 0.5f;

    [Header("達成時サウンド（SE3）")]
    [SerializeField, Tooltip("成功（ロック）に入った瞬間に一度だけ再生")]
    private AudioClip m_SeGoal;

    [Header("ロック時サウンド（SE4）")]
    [SerializeField, Tooltip("ロック後に操作しようとした時に鳴らすSE")]
    private AudioClip m_SeBlocked;

    //========================
    // 回転安定化：基準姿勢＋指令角（X/Y/Z）保持
    //========================
    [Header("=== 回転合成設定 ===")]
    [SerializeField, Tooltip("合成順序（Yaw→Pitch→Roll の想定）")]
    private RotationOrder m_Order = RotationOrder.YXZ;

    private Quaternion m_BaseLocalRotation; // 起動時の基準
    private float m_CmdAngleX;              // 指令角（度）
    private float m_CmdAngleY;
    private float m_CmdAngleZ;

    //=== 公開API：達成状態とイベント =========================
    public bool IsLocked => m_IsLocked;                    // 達成(ロック)状態を公開
    public event Action<bool> OnLockedChanged;             // true=達成した瞬間に発火
    //======================================================

    private enum RotationOrder
    {
        XYZ,
        XZY,
        YXZ,
        YZX,
        ZXY,
        ZYX,
    }

    //========================
    // ランタイム
    //========================
    private bool m_IsLocked;

    //========================
    // ライフサイクル
    //========================
    private void Awake()
    {
        if (m_AudioSource == null)
        {
            m_AudioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // 回転ランタイム初期化
        if (m_RotationA != null) m_RotationA.ResetRuntime();
        if (m_RotationB != null) m_RotationB.ResetRuntime();

        // 基準姿勢を保存（現在のlocalRotationを基準とし、指令角は0スタート）
        m_BaseLocalRotation = transform.localRotation;
        m_CmdAngleX = m_CmdAngleY = m_CmdAngleZ = 0f;

        // 初期状態で達成している場合もこの時点でロック
        CheckAndUpdateLock();
    }

    private void Update()
    {
        if (m_IsLocked)
        {
            if (AnyControlKeyDown())
            {
                StopAllSe();
                PlayOneShotSafe(m_SeBlocked);
            }
            return;
        }

        // ワールドワープ（長押し禁止）
        HandleMoveKey(in m_MoveRight);
        HandleMoveKey(in m_MoveLeft);
        HandleMoveKey(in m_MoveUp);
        HandleMoveKey(in m_MoveDown);

        // 回転（固定2系統・ローカル） ※合成は毎回「基準×固定順序」
        HandleFixedRotation(m_RotationA);
        HandleFixedRotation(m_RotationB);

        // 条件達成チェック
        CheckAndUpdateLock();
    }

    private void OnValidate()
    {
        // クランプ範囲（位置）の正規化
        if (m_ClampX.Enabled && m_ClampX.Min > m_ClampX.Max) { float t = m_ClampX.Min; m_ClampX.Min = m_ClampX.Max; m_ClampX.Max = t; }
        if (m_ClampY.Enabled && m_ClampY.Min > m_ClampY.Max) { float t = m_ClampY.Min; m_ClampY.Min = m_ClampY.Max; m_ClampY.Max = t; }
        if (m_ClampZ.Enabled && m_ClampZ.Min > m_ClampZ.Max) { float t = m_ClampZ.Min; m_ClampZ.Min = m_ClampZ.Max; m_ClampZ.Max = t; }

        // 完了“位置”範囲の正規化
        if (m_GoalPosXRange.Enabled && m_GoalPosXRange.Min > m_GoalPosXRange.Max) { float t = m_GoalPosXRange.Min; m_GoalPosXRange.Min = m_GoalPosXRange.Max; m_GoalPosXRange.Max = t; }
        if (m_GoalPosYRange.Enabled && m_GoalPosYRange.Min > m_GoalPosYRange.Max) { float t = m_GoalPosYRange.Min; m_GoalPosYRange.Min = m_GoalPosYRange.Max; m_GoalPosYRange.Max = t; }
        if (m_GoalPosZRange.Enabled && m_GoalPosZRange.Min > m_GoalPosZRange.Max) { float t = m_GoalPosZRange.Min; m_GoalPosZRange.Min = m_GoalPosZRange.Max; m_GoalPosZRange.Max = t; }
        // 角度範囲（Min>Max）は wrap として扱うので入れ替えはしない
    }

    private static bool AngleApproximately(float aDeg, float bDeg, float epsDeg)
    {
        return Mathf.Abs(Mathf.DeltaAngle(aDeg, bDeg)) <= epsDeg;
    }

    private float GetCommandedAngle(Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return m_CmdAngleX;
            case Axis.Y: return m_CmdAngleY;
            default: return m_CmdAngleZ;
        }
    }


    //========================
    // 入力ユーティリティ（ロック中の検出用）
    //========================
    private bool AnyControlKeyDown()
    {
        if (Input.GetKeyDown(m_MoveRight.Key) || PadBool.IsRightStickRight()) return true;
        if (Input.GetKeyDown(m_MoveLeft.Key) || PadBool.IsRightStickLeft()) return true;
        if (Input.GetKeyDown(m_MoveUp.Key) || PadBool.IsRightStickUp()) return true;
        if (Input.GetKeyDown(m_MoveDown.Key) || PadBool.IsRightStickDown()) return true;

        if (m_RotationA != null && m_RotationA.Key != KeyCode.None && Input.GetKeyDown(m_RotationA.Key)) return true;
        if (m_RotationB != null && m_RotationB.Key != KeyCode.None && Input.GetKeyDown(m_RotationB.Key)) return true;

        return false;
    }

    //========================
    // ワープ処理（ワールド）
    //========================
    private void HandleMoveKey(in MoveKey mk)
    {
        if (mk.Key == KeyCode.None) return;

        if (Input.GetKeyDown(mk.Key))
        {
            if (m_IsLocked)
            {
                StopAllSe();
                PlayOneShotSafe(m_SeBlocked);
                return;
            }

            Vector3 dir = mk.WorldDirection;
            if (dir.sqrMagnitude <= 0f) return;

            dir.Normalize();
            Vector3 oldPos = transform.position;
            Vector3 newPos = oldPos + dir * mk.Step;

            // クランプ（ワールド）
            newPos = ApplyClampWorld(newPos);

            if ((newPos - oldPos).sqrMagnitude > 1e-8f)
            {
                transform.position = newPos;
                PlayOneShotSafe(m_SeMove);

                CheckAndUpdateLock();
                if (m_IsLocked)
                {
                    return;
                }
            }
        }
    }

    private Vector3 ApplyClampWorld(Vector3 pos)
    {
        if (m_ClampX.Enabled) pos.x = Mathf.Clamp(pos.x, m_ClampX.Min, m_ClampX.Max);
        if (m_ClampY.Enabled) pos.y = Mathf.Clamp(pos.y, m_ClampY.Min, m_ClampY.Max);
        if (m_ClampZ.Enabled) pos.z = Mathf.Clamp(pos.z, m_ClampZ.Min, m_ClampZ.Max);
        return pos;
    }

    //========================
    // 回転処理（固定2系統・ローカル・ピンポン）
    //========================
    private void HandleFixedRotation(FixedRotation rot)
    {
        if (rot == null || rot.Key == KeyCode.None) return;
        if (!Input.GetKeyDown(rot.Key)) return;

        if (m_IsLocked)
        {
            StopAllSe();
            PlayOneShotSafe(m_SeBlocked);
            return;
        }

        int count = (rot.Angles != null) ? rot.Angles.Length : 0;
        if (count <= 0) return;

        int last = count - 1;

        // === どのインデックスを今回「適用」するか決定 ===
        int idxToApply;
        if (!rot._appliedOnce && rot.StartFromFirst)
        {
            // まず A[0]
            idxToApply = 0;

            // 先頭が現在角と同じなら A[1] から開始（見た目変化なしを回避）
            if (rot.SkipFirstIfSame && last >= 1)
            {
                float target0 = rot.Angles[0];
                float current = GetCommandedAngle(rot.Axis);
                if (AngleApproximately(target0, current, rot.FirstPressEps))
                {
                    idxToApply = 1;
                }
            }
        }
        else
        {
            // 2回目以降はランタイムの _index をそのまま使う
            idxToApply = Mathf.Clamp(rot._index, 0, last);
        }

        // === 適用 ===
        float angle = NormalizeAngle(rot.Angles[idxToApply]);
        SetCommandedAngle(rot.Axis, angle);
        RebuildLocalRotation();
        PlayOneShotSafe(rot.Se);

        // === 次回用に ping-pong 進行位置を更新 ===
        if (last >= 1)
        {
            // 端で反転するピンポン
            int dir = rot._direction;
            if (idxToApply <= 0) dir = +1;
            else if (idxToApply >= last) dir = -1;

            rot._direction = dir;
            rot._index = Mathf.Clamp(idxToApply + dir, 0, last);
        }
        else
        {
            rot._index = 0;
            rot._direction = +1;
        }

        rot._appliedOnce = true;

        CheckAndUpdateLock();
    }


    private void SetCommandedAngle(Axis axis, float angleDeg)
    {
        switch (axis)
        {
            case Axis.X: m_CmdAngleX = angleDeg; break;
            case Axis.Y: m_CmdAngleY = angleDeg; break;
            case Axis.Z: m_CmdAngleZ = angleDeg; break;
        }
    }

    private void RebuildLocalRotation()
    {
        // 指令角からクォータニオンを固定順序で合成
        Quaternion qx = Quaternion.AngleAxis(m_CmdAngleX, Vector3.right);
        Quaternion qy = Quaternion.AngleAxis(m_CmdAngleY, Vector3.up);
        Quaternion qz = Quaternion.AngleAxis(m_CmdAngleZ, Vector3.forward);

        Quaternion r;
        switch (m_Order)
        {
            case RotationOrder.XYZ: r = qx * qy * qz; break;
            case RotationOrder.XZY: r = qx * qz * qy; break;
            case RotationOrder.YXZ: r = qy * qx * qz; break; // 既定：Yaw→Pitch→Roll
            case RotationOrder.YZX: r = qy * qz * qx; break;
            case RotationOrder.ZXY: r = qz * qx * qy; break;
            default: r = qz * qy * qx; break; // ZYX
        }

        transform.localRotation = m_BaseLocalRotation * r;
    }

    private static float GetCycleAngle(FixedRotation r, int index)
    {
        if (r == null || r.Angles == null || r.Angles.Length == 0) return 0f;
        index = Mathf.Clamp(index, 0, r.Angles.Length - 1);
        return r.Angles[index];
    }

    private static float NormalizeAngle(float a)
    {
        return Mathf.Repeat(a, 360f); // 0..360
    }

    //========================
    // 達成ロック（位置＆回転の“すべて”を満たしたら）
    //========================
    private void CheckAndUpdateLock()
    {
        if (m_IsLocked) return;

        bool posOk = EvaluatePositionGoal(out bool hasPos);
        bool rotOk = EvaluateRotationGoal(out bool hasRot);

        if (hasPos && hasRot && posOk && rotOk)
        {
            m_IsLocked = true;

            // 一度だけ成功SEを再生
            PlayOneShotSafe(m_SeGoal);

            // 達成した瞬間に通知
            OnLockedChanged?.Invoke(true);
        }
    }

    private bool EvaluatePositionGoal(out bool hasActive)
    {
        Vector3 p = transform.position;

        hasActive = false;
        bool ok = true;

        if (m_GoalPosXRange.Enabled)
        {
            hasActive = true;
            ok &= IsValueInRange(p.x, m_GoalPosXRange.Min, m_GoalPosXRange.Max, m_PositionEpsilon);
        }
        if (m_GoalPosYRange.Enabled)
        {
            hasActive = true;
            ok &= IsValueInRange(p.y, m_GoalPosYRange.Min, m_GoalPosYRange.Max, m_PositionEpsilon);
        }
        if (m_GoalPosZRange.Enabled)
        {
            hasActive = true;
            ok &= IsValueInRange(p.z, m_GoalPosZRange.Min, m_GoalPosZRange.Max, m_PositionEpsilon);
        }

        return ok;
    }

    private bool EvaluateRotationGoal(out bool hasActive)
    {
        hasActive = false;

        Transform t = (m_RotationGoalTarget != null) ? m_RotationGoalTarget : this.transform;
        if (t == null) return false;

        Vector3 le = t.localEulerAngles; // ローカル角（※表示角は分解順序に依存）
        bool ok = true;

        if (m_GoalRotXRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(le.x, m_GoalRotXRange.Min, m_GoalRotXRange.Max, m_AngleEpsilon);
        }
        if (m_GoalRotYRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(le.y, m_GoalRotYRange.Min, m_GoalRotYRange.Max, m_AngleEpsilon);
        }
        if (m_GoalRotZRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(le.z, m_GoalRotZRange.Min, m_GoalRotZRange.Max, m_AngleEpsilon);
        }

        return ok;
    }

    // 値の範囲判定（誤差つき）
    private static bool IsValueInRange(float v, float min, float max, float eps)
    {
        float lo = Mathf.Min(min, max) - eps;
        float hi = Mathf.Max(min, max) + eps;
        return v >= lo && v <= hi;
    }

    // 角度の範囲判定（0..360 正規化、wrap対応、誤差つき）
    private static bool IsAngleInRange(float angleDeg, float minDeg, float maxDeg, float eps)
    {
        float angle = NormalizeAngle(angleDeg);
        float min = NormalizeAngle(minDeg);
        float max = NormalizeAngle(maxDeg);

        if (Mathf.Approximately(min, max))
        {
            return Mathf.Abs(Mathf.DeltaAngle(angle, min)) <= eps;
        }

        if (min <= max)
        {
            return angle >= (min - eps) && angle <= (max + eps);
        }
        else
        {
            return angle >= (min - eps) || angle <= (max + eps);
        }
    }

    //========================
    // オーディオ
    //========================
    private void PlayOneShotSafe(AudioClip clip)
    {
        if (clip == null) return;

        if (m_AudioSource != null)
        {
            m_AudioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    private void StopAllSe()
    {
        if (m_AudioSource != null)
        {
            m_AudioSource.Stop();
        }
    }
}
