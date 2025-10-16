using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollisionManager : MonoBehaviour
{
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

        [Range(-180f, 180f)] public float Min;
        [Range(-180f, 180f)] public float Max;

        public bool IsActive => Enabled;

        /// <summary>
        /// 角度deg（任意表現）を -180..180 に正規化し、Min..Max（-180..180、wrap対応）に含まれるか。
        /// Min &lt;= Max: 通常、Min &gt; Max: -180/180跨ぎ（例: 170..-170）。
        /// </summary>
        public static bool Contains(float deg, in AngleRange r)
        {
            if (!r.Enabled) return true;

            float a = ToSigned180(deg);
            float min = ToSigned180(r.Min);
            float max = ToSigned180(r.Max);

            if (Mathf.Approximately(min, max))
            {
                // 一点一致（±0）。必要なら判定誤差を拡張側に持たせてください。
                return Mathf.Abs(Mathf.DeltaAngle(a, min)) <= 0f;
            }

            if (min <= max)
            {
                // 通常区間
                return a >= min && a <= max;
            }
            else
            {
                // wrap区間: 例 170..-170 → [-180..-170] ∪ [170..180]
                return (a >= min) || (a <= max);
            }
        }

        public static float ToSigned180(float x) => Mathf.DeltaAngle(0f, x);
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

    // ----- 条件：Euler角（-180..180指定） -----
    [Serializable]
    public sealed class EulerCondition
    {
        [Header("Euler角条件を使う")]
        public bool Enabled;

        [Tooltip("true=localEulerAngles / false=eulerAngles（ワールド）")]
        public bool UseLocalEuler;

        [Header("各軸角度（-180..180）。使う軸だけ Enabled をON（wrap対応）")]
        public AngleRange X, Y, Z;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            // Unityは内部0..360表現。ここで -180..180 に揃える
            Vector3 eRaw = UseLocalEuler ? t.localEulerAngles : t.eulerAngles;
            Vector3 e = new Vector3(AngleRange.ToSigned180(eRaw.x),
                                    AngleRange.ToSigned180(eRaw.y),
                                    AngleRange.ToSigned180(eRaw.z));

            if (debug)
            {
                Debug.Log($"[CM] Euler {(UseLocalEuler ? "Local" : "World")} (signed)=({e.x:F1},{e.y:F1},{e.z:F1})  " +
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
            if (!(Position.Enabled || Euler.Enabled)) return false;
            if (!Position.IsSatisfied(mirror, debug)) return false;
            if (!Euler.IsSatisfied(mirror, debug)) return false;
            return true;
        }

        public void ApplyDelta(bool matchedNow)
        {
            if (!_initialized || matchedNow != _lastMatched)
            {
                if (matchedNow)
                {
                    EnableOnMatch?.SetEnabled(true);
                    DisableOnMatch?.SetEnabled(false);
                }
                else
                {
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

        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            var entry = m_Mirrors[i];
            if (entry == null || entry.Mirror == null || entry.Rules == null) continue;

            var rules = entry.Rules;
            int n = rules.Length;
            if (n == 0) continue;

            // 1) 評価
            bool[] matched = new bool[n];
            int firstStopIndex = -1;
            for (int r = 0; r < n; ++r)
            {
                var rule = rules[r];
                if (rule == null) continue;

                bool debug = (m_Mirrors.Length == 1 && entry.Rules.Length == 1 && m_DebugLog);
                matched[r] = rule.Evaluate(entry.Mirror, debug);

                if (firstStopIndex < 0 && matched[r] && rule.StopAfterApply)
                    firstStopIndex = r;
            }
            if (firstStopIndex >= 0)
            {
                for (int r = firstStopIndex + 1; r < n; ++r) matched[r] = false;
            }

            // 2) 適用（変化のみ）
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
