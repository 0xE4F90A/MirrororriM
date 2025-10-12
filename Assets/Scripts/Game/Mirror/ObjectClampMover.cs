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
        [Tooltip("この軸の“完了角度範囲”判定を有効化")]
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

    [Serializable]
    public sealed class RotationEntry
    {
        [Header("対象")]
        [Tooltip("回転させる Transform（未指定なら自身）")]
        public Transform Target;

        [Tooltip("回転させるワールド軸（X/Y/Z）")]
        public Axis Axis = Axis.Y;

        [Header("角度列（ピンポン）")]
        [Tooltip("例：0,-45,-90 → 0→-45→-90→-45→0…／0,90 → 0→90→0→90…")]
        public float[] Angles = new float[] { 0f, 90f };

        [Tooltip("開始時に最初の角度を適用")]
        public bool ApplyFirstAngleOnStart = false;

        // ランタイム
        [NonSerialized] public int _index = 0;
        [NonSerialized] public int _direction = 1;

        public void ResetRuntime()
        {
            _index = 0;
            _direction = 1;
        }
    }

    [Serializable]
    public sealed class RotationGroup
    {
        [Header("入力（このキーで本グループの全エントリを同時切替）")]
        [Tooltip("押下ごとに全エントリの角度を1ステップ進める（ピンポン）")]
        public KeyCode Key = KeyCode.None;

        [Header("サウンド")]
        [Tooltip("押下ごとに再生するSE（例：SE2やSE3）")]
        public AudioClip Se;

        [Header("回転エントリ（複数可）")]
        [Tooltip("同じキーで複数軸・複数対象を同時に切り替え可能")]
        public RotationEntry[] Entries;

        public void ResetRuntimeAll()
        {
            if (Entries == null) return;
            for (int i = 0; i < Entries.Length; ++i)
            {
                if (Entries[i] != null) Entries[i].ResetRuntime();
            }
        }
    }

    //========================
    // インスペクター：移動・クランプ
    //========================
    [Header("=== ワープ（ワールド座標・GetKeyDownのみ） ===")]
    [SerializeField] private MoveKey m_MoveRight = new MoveKey { Key = KeyCode.RightArrow, WorldDirection = new Vector3(1f, 0f, 0f), Step = 40f };
    [SerializeField] private MoveKey m_MoveLeft = new MoveKey { Key = KeyCode.LeftArrow, WorldDirection = new Vector3(-1f, 0f, 0f), Step = 40f };
    [SerializeField] private MoveKey m_MoveUp = new MoveKey { Key = KeyCode.UpArrow, WorldDirection = new Vector3(0f, 0f, 1f), Step = 40f };
    [SerializeField] private MoveKey m_MoveDown = new MoveKey { Key = KeyCode.DownArrow, WorldDirection = new Vector3(0f, 0f, -1f), Step = 40f };

    [Header("移動SE（SE1）")]
    [SerializeField, Tooltip("移動時に再生するSE")] private AudioClip m_SeMove;
    [SerializeField, Tooltip("SE再生に使うAudioSource（必ず割り当て推奨）")] private AudioSource m_AudioSource;

    [Header("=== ワールド座標クランプ ===")]
    [SerializeField] private AxisClamp m_ClampX = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampY = new AxisClamp { Enabled = false, Min = 0f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampZ = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };

    //========================
    // インスペクター：回転グループ（操作用）
    //========================
    [Header("=== 回転グループ（複数可・各キーで同時切替） ===")]
    [Tooltip("例）Yキーで ①Y軸[0,-45,-90] ②Z軸[0,45,90] を同時に切替")]
    [SerializeField] private RotationGroup[] m_RotationGroups;

    //========================
    // インスペクター：完了ロック条件（位置＆回転“範囲”）
    //========================
    [Header("=== 完了ロック条件（位置“範囲”/ワールド） ===")]
    [SerializeField] private AxisGoalRange m_GoalPosXRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AxisGoalRange m_GoalPosYRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AxisGoalRange m_GoalPosZRange = new AxisGoalRange { Enabled = false, Min = 0f, Max = 0f };

    [Header("=== 完了ロック条件（回転“範囲”/ワールド角） ===")]
    [SerializeField, Tooltip("回転完了範囲の評価対象（未指定なら自身）")]
    private Transform m_RotationGoalTarget;

    [SerializeField] private AngleGoalRange m_GoalRotXRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AngleGoalRange m_GoalRotYRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };
    [SerializeField] private AngleGoalRange m_GoalRotZRange = new AngleGoalRange { Enabled = false, Min = 0f, Max = 0f };

    [Header("許容誤差（範囲端のマージン）")]
    [SerializeField, Tooltip("位置範囲の端に対するマージン（m）")] private float m_PositionEpsilon = 0.01f;
    [SerializeField, Tooltip("角度範囲の端に対するマージン（度）")] private float m_AngleEpsilon = 0.5f;

    [Header("ロック時サウンド（SE4）")]
    [SerializeField, Tooltip("ロック後に操作しようとした時に鳴らすSE")]
    private AudioClip m_SeBlocked;

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
        // 回転初期角度の適用とランタイム初期化
        if (m_RotationGroups != null)
        {
            for (int g = 0; g < m_RotationGroups.Length; ++g)
            {
                var group = m_RotationGroups[g];
                if (group?.Entries == null) continue;

                for (int i = 0; i < group.Entries.Length; ++i)
                {
                    var e = group.Entries[i];
                    if (e == null) continue;

                    e.ResetRuntime();

                    if (e.ApplyFirstAngleOnStart)
                    {
                        Transform t = (e.Target != null) ? e.Target : this.transform;
                        if (t != null && e.Angles != null && e.Angles.Length > 0)
                        {
                            float a0 = NormalizeAngle(e.Angles[0]);
                            ApplyWorldAxisAngle(t, e.Axis, a0);
                        }
                    }
                }
            }
        }

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

        // 回転グループ切替
        if (m_RotationGroups != null)
        {
            for (int g = 0; g < m_RotationGroups.Length; ++g)
            {
                HandleRotationGroup(m_RotationGroups[g]);
            }
        }

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

    //========================
    // 入力ユーティリティ（ロック中の検出用）
    //========================
    private bool AnyControlKeyDown()
    {
        if (Input.GetKeyDown(m_MoveRight.Key)) return true;
        if (Input.GetKeyDown(m_MoveLeft.Key)) return true;
        if (Input.GetKeyDown(m_MoveUp.Key)) return true;
        if (Input.GetKeyDown(m_MoveDown.Key)) return true;

        if (m_RotationGroups != null)
        {
            for (int g = 0; g < m_RotationGroups.Length; ++g)
            {
                var rg = m_RotationGroups[g];
                if (rg != null && rg.Key != KeyCode.None && Input.GetKeyDown(rg.Key)) return true;
            }
        }
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
    // 回転処理（ワールド・ピンポン）
    //========================
    private void HandleRotationGroup(RotationGroup group)
    {
        if (group == null || group.Key == KeyCode.None) return;
        if (!Input.GetKeyDown(group.Key)) return;

        if (m_IsLocked)
        {
            StopAllSe();
            PlayOneShotSafe(m_SeBlocked);
            return;
        }

        bool appliedAny = false;

        if (group.Entries != null)
        {
            for (int i = 0; i < group.Entries.Length; ++i)
            {
                var e = group.Entries[i];
                if (e == null || e.Angles == null || e.Angles.Length < 2) continue;

                Transform t = (e.Target != null) ? e.Target : this.transform;
                if (t == null) continue;

                // 現インデックスの角度を適用（ワールド）
                float angle = NormalizeAngle(GetCycleAngle(e, e._index));
                ApplyWorldAxisAngle(t, e.Axis, angle);
                appliedAny = true;

                // 次インデックスへ（端で反転）
                int last = e.Angles.Length - 1;
                if (e._index <= 0) e._direction = +1;
                else if (e._index >= last) e._direction = -1;
                e._index += e._direction;
            }
        }

        if (appliedAny)
        {
            PlayOneShotSafe(group.Se);
            CheckAndUpdateLock();
        }
    }

    private static float GetCycleAngle(RotationEntry e, int index)
    {
        if (e.Angles == null || e.Angles.Length == 0) return 0f;
        index = Mathf.Clamp(index, 0, e.Angles.Length - 1);
        return e.Angles[index];
    }

    private static float NormalizeAngle(float a)
    {
        return Mathf.Repeat(a, 360f); // 0..360
    }

    private static void ApplyWorldAxisAngle(Transform t, Axis axis, float angleDeg)
    {
        Vector3 worldEuler = t.eulerAngles; // ワールド
        switch (axis)
        {
            case Axis.X:
                {
                    worldEuler.x = angleDeg;
                    break;
                }
            case Axis.Y:
                {
                    worldEuler.y = angleDeg;
                    break;
                }
            case Axis.Z:
                {
                    worldEuler.z = angleDeg;
                    break;
                }
        }
        t.rotation = Quaternion.Euler(worldEuler); // ワールド適用
    }

    //========================
    // 達成ロック（位置＆回転の“すべて”を満たしたら）
    //========================
    private void CheckAndUpdateLock()
    {
        if (m_IsLocked) return;

        bool posOk = EvaluatePositionGoal(out bool hasPos);
        bool rotOk = EvaluateRotationGoal(out bool hasRot);

        // 位置にも回転にも“有効な範囲”が存在し、かつ両者とも満たされている場合のみロック
        if (hasPos && hasRot && posOk && rotOk)
        {
            m_IsLocked = true;
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

        Vector3 we = t.eulerAngles; // ワールド

        bool ok = true;

        if (m_GoalRotXRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(we.x, m_GoalRotXRange.Min, m_GoalRotXRange.Max, m_AngleEpsilon);
        }
        if (m_GoalRotYRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(we.y, m_GoalRotYRange.Min, m_GoalRotYRange.Max, m_AngleEpsilon);
        }
        if (m_GoalRotZRange.Enabled)
        {
            hasActive = true;
            ok &= IsAngleInRange(we.z, m_GoalRotZRange.Min, m_GoalRotZRange.Max, m_AngleEpsilon);
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
            // 点指定：±eps で判定
            return Mathf.Abs(Mathf.DeltaAngle(angle, min)) <= eps;
        }

        if (min <= max)
        {
            return angle >= (min - eps) && angle <= (max + eps);
        }
        else
        {
            // wrap: [min..360) ∪ [0..max]
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
            // ※ ロック時に完全停止させたい場合は m_AudioSource を必ず割り当ててください。
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
