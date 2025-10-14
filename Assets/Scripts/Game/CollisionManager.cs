using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollisionManager : MonoBehaviour
{
    public enum ReferenceAxis { Forward, Right, Up }
    public enum ProjectPlane { None, XY, XZ, YZ }

    [Serializable]
    public struct FloatRange
    {
        [Tooltip("この軸で判定する場合はON")]
        public bool Enabled;
        [Tooltip("最小値（Min > MaxでもOK。内部で入替）")]
        public float Min;
        [Tooltip("最大値（Min > MaxでもOK。内部で入替）")]
        public float Max;

        public bool IsActive => Enabled;

        public static bool Contains(float v, in FloatRange r, float epsilon = 0f)
        {
            if (!r.Enabled) return true;
            float min = r.Min, max = r.Max;
            if (min > max) { var t = min; min = max; max = t; }
            return v >= (min - epsilon) && v <= (max + epsilon);
        }
    }


    [Serializable]
    public struct AngleRange
    {
        [Tooltip("この軸で判定する場合はON")]
        public bool Enabled;
        [Range(0f, 360f)] public float Min;
        [Range(0f, 360f)] public float Max;

        public bool IsActive => Enabled;

        public static bool Contains(float deg, in AngleRange r)
        {
            if (!r.Enabled) return true;
            float a = deg % 360f; if (a < 0f) a += 360f;
            if (r.Min <= r.Max) return a >= r.Min && a <= r.Max;
            return a >= r.Min || a <= r.Max; // 0度またぎ
        }
    }

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
                for (int i = 0; i < Colliders.Length; ++i)
                    if (Colliders[i]) Colliders[i].enabled = enabled;

            if (Objects != null)
                for (int i = 0; i < Objects.Length; ++i)
                    if (Objects[i]) Objects[i].SetActive(enabled);
        }
    }

    // ----- 条件：位置 -----
    [Serializable]
    public sealed class PositionCondition
    {
        [Header("位置条件を使う")]
        public bool Enabled;

        [Tooltip("null=ワールド基準 / 指定あり=このTransformローカル基準")]
        public Transform Space;

        [Header("X / Y / Z のレンジ（使う軸だけ Enabled をON）")]
        public FloatRange X, Y, Z;

        [Header("判定誤差（±）")]
        [Min(0f)] public float Epsilon = 0.001f;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            Vector3 p = (Space ? Space.InverseTransformPoint(t.position) : t.position);

            if (debug)
            {
                Debug.Log($"[CM] Pos {(Space ? $"Local:{Space.name}" : "World")} " +
                          $"= ({p.x:F4},{p.y:F4},{p.z:F4})  " +
                          $"X[{X.Min},{X.Max}]({X.Enabled}) Y[{Y.Min},{Y.Max}]({Y.Enabled}) Z[{Z.Min},{Z.Max}]({Z.Enabled})  ±{Epsilon}");
            }

            return FloatRange.Contains(p.x, X, Epsilon)
                && FloatRange.Contains(p.y, Y, Epsilon)
                && FloatRange.Contains(p.z, Z, Epsilon);
        }
    }

    // ----- 条件：向き（ベクトル成分） -----
    [Serializable]
    public sealed class DirectionCondition
    {
        [Header("向き条件を使う")]
        public bool Enabled;

        [Header("どのローカル軸を“向き”とするか")]
        public ReferenceAxis Axis = ReferenceAxis.Forward;

        [Header("平面投影（3Dそのままなら None）")]
        public ProjectPlane Project = ProjectPlane.XZ;

        [Header("各成分レンジ（-1〜+1）。使う軸だけ Enabled をON")]
        public FloatRange X, Y, Z;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            Vector3 v = Axis switch
            {
                ReferenceAxis.Right => t.right,
                ReferenceAxis.Up => t.up,
                _ => t.forward
            };

            switch (Project)
            {
                case ProjectPlane.XY: v.z = 0f; break;
                case ProjectPlane.XZ: v.y = 0f; break;
                case ProjectPlane.YZ: v.x = 0f; break;
            }

            if (v.sqrMagnitude <= 1e-8f) return false;
            v.Normalize();

            if (debug)
            {
                Debug.Log($"[CM] Dir({Axis}, {Project})=({v.x:F3},{v.y:F3},{v.z:F3})  " +
                          $"XR[{X.Min},{X.Max}]({X.Enabled}) YR[{Y.Min},{Y.Max}]({Y.Enabled}) ZR[{Z.Min},{Z.Max}]({Z.Enabled})");
            }

            return FloatRange.Contains(v.x, X)
                && FloatRange.Contains(v.y, Y)
                && FloatRange.Contains(v.z, Z);
        }
    }

    // ----- 条件：Euler角 -----
    [Serializable]
    public sealed class EulerCondition
    {
        [Header("Euler角条件を使う")]
        public bool Enabled;

        [Tooltip("true=localEulerAngles / false=eulerAngles（ワールド）")]
        public bool UseLocalEuler;

        [Header("各軸角度（度）。使う軸だけ Enabled をON")]
        public AngleRange X, Y, Z;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            Vector3 e = UseLocalEuler ? t.localEulerAngles : t.eulerAngles;
            if (debug)
            {
                Debug.Log($"[CM] Euler {(UseLocalEuler ? "Local" : "World")}=({e.x:F1},{e.y:F1},{e.z:F1})  " +
                          $"XR[{X.Min},{X.Max}]({X.Enabled}) YR[{Y.Min},{Y.Max}]({Y.Enabled}) ZR[{Z.Min},{Z.Max}]({Z.Enabled})");
            }
            return AngleRange.Contains(e.x, X)
                && AngleRange.Contains(e.y, Y)
                && AngleRange.Contains(e.z, Z);
        }
    }

    [Serializable]
    public sealed class Rule
    {
        [Header("=== 条件（ONのもの全てを満たしたら成立） ===")]
        public PositionCondition Position;
        public DirectionCondition Direction;
        public EulerCondition Euler;

        [Header("=== 成立時の操作 ===")]
        [Tooltip("上の条件にマッチしたら『有効化』する対象")]
        public ToggleGroup EnableOnMatch;
        [Tooltip("上の条件にマッチしたら『無効化』する対象")]
        public ToggleGroup DisableOnMatch;

        [Tooltip("このルールが成立したら以降のルールを評価しない（先勝ち）。ただし“戻し処理”は維持されます。")]
        public bool StopAfterApply;

        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _lastMatched;

        public bool Evaluate(Transform mirror, bool debug)
        {
            if (!(Position.Enabled || Direction.Enabled || Euler.Enabled)) return false;
            if (!Position.IsSatisfied(mirror, debug)) return false;
            if (!Direction.IsSatisfied(mirror, debug)) return false;
            if (!Euler.IsSatisfied(mirror, debug)) return false;
            return true;
        }

        public void ApplyDelta(bool matchedNow)
        {
            // 初回は現在の状態に合わせて適用
            if (!_initialized || matchedNow != _lastMatched)
            {
                if (matchedNow)
                {
                    // 条件を満たしたので：有効化 / 無効化
                    EnableOnMatch?.SetEnabled(true);
                    DisableOnMatch?.SetEnabled(false);
                }
                else
                {
                    // 条件を満たさないので：元に戻す（逆適用）
                    EnableOnMatch?.SetEnabled(false);
                    DisableOnMatch?.SetEnabled(true);
                }

                _lastMatched = matchedNow;
                _initialized = true;
            }
        }
    }

    [Serializable]
    public sealed class MirrorEntry
    {
        [Header("鏡（Transform を割り当て）")]
        public Transform Mirror;

        [Header("この鏡に対するルール（上から順に評価）")]
        public Rule[] Rules;
    }

    [Header("監視する鏡の一覧")]
    [SerializeField] private MirrorEntry[] m_Mirrors;

    [Header("デバッグログを出す")]
    [SerializeField] private bool m_DebugLog = false;

    private void Update()
    {
        if (m_Mirrors == null) return;

        // 各鏡ごとに2パス：評価 → 適用
        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            var entry = m_Mirrors[i];
            if (entry == null || entry.Mirror == null || entry.Rules == null) continue;

            var rules = entry.Rules;
            int n = rules.Length;
            if (n == 0) continue;

            // 1) すべての一致結果を計算
            bool[] matched = new bool[n];
            int firstStopIndex = -1;
            for (int r = 0; r < n; ++r)
            {
                var rule = rules[r];
                if (rule == null) continue;

                matched[r] = rule.Evaluate(entry.Mirror, m_Mirrors.Length == 1 && m_Mirrors[0].Rules.Length == 1 && m_DebugLog);

                if (firstStopIndex < 0 && matched[r] && rule.StopAfterApply)
                {
                    firstStopIndex = r;
                }
            }
            // 先勝ち指定があれば、その後ろの一致は無視（＝戻しの対象）
            if (firstStopIndex >= 0)
            {
                for (int r = firstStopIndex + 1; r < n; ++r) matched[r] = false;
            }

            // 2) 変化があるものだけ適用（不一致→戻し / 一致→適用）
            for (int r = 0; r < n; ++r)
            {
                var rule = rules[r];
                if (rule == null) continue;
                rule.ApplyDelta(matched[r]);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (m_Mirrors == null) return;

        foreach (var entry in m_Mirrors)
        {
            if (entry == null || entry.Rules == null) continue;

            foreach (var rule in entry.Rules)
            {
                if (rule == null || !rule.Position.Enabled) continue;

                var pos = rule.Position;

                float xMin = pos.X.Enabled ? Mathf.Min(pos.X.Min, pos.X.Max) : 0f;
                float xMax = pos.X.Enabled ? Mathf.Max(pos.X.Min, pos.X.Max) : 0f;
                float yMin = pos.Y.Enabled ? Mathf.Min(pos.Y.Min, pos.Y.Max) : 0f;
                float yMax = pos.Y.Enabled ? Mathf.Max(pos.Y.Min, pos.Y.Max) : 0f;
                float zMin = pos.Z.Enabled ? Mathf.Min(pos.Z.Min, pos.Z.Max) : 0f;
                float zMax = pos.Z.Enabled ? Mathf.Max(pos.Z.Min, pos.Z.Max) : 0f;

                Vector3 center = new Vector3((xMin + xMax) * 0.5f,
                                             (yMin + yMax) * 0.5f,
                                             (zMin + zMax) * 0.5f);
                Vector3 size = new Vector3(Mathf.Abs(xMax - xMin),
                                             Mathf.Abs(yMax - yMin),
                                             Mathf.Abs(zMax - zMin));

                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = pos.Space ? pos.Space.localToWorldMatrix : Matrix4x4.identity;

                Gizmos.color = new Color(0f, 1f, 1f, 1f * 0.15f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(center, size);

                Gizmos.matrix = prev;
            }
        }
    }
#endif
}
