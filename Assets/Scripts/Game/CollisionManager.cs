using System;
using System.Collections.Generic;
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
            float min = r.Min;
            float max = r.Max;
            if (min > max)
            {
                var t = min;
                min = max;
                max = t;
            }
            return v >= (min - epsilon) && v <= (max + epsilon);
        }
    }

    [Serializable]
    public struct AngleRange
    {
        [Tooltip("この軸で判定する場合はON")]
        public bool Enabled;

        [Range(-180f, 180f)]
        public float Min;
        [Range(-180f, 180f)]
        public float Max;

        public bool IsActive => Enabled;

        /// <summary>
        /// 角度degを -180..180 に正規化し、Min..Max に入っているか判定。
        /// Min <= Max は通常、Min > Max は -180/180 を跨ぐラップ区間。
        /// </summary>
        public static bool Contains(float deg, in AngleRange r)
        {
            if (!r.Enabled) return true;

            float a = ToSigned180(deg);
            float min = ToSigned180(r.Min);
            float max = ToSigned180(r.Max);

            if (Mathf.Approximately(min, max))
            {
                // 完全一致だけ許容
                return Mathf.Abs(Mathf.DeltaAngle(a, min)) <= 0f;
            }

            if (min <= max)
            {
                // 通常の区間
                return a >= min && a <= max;
            }
            else
            {
                // ラップ区間 (例 170..-170)
                return (a >= min) || (a <= max);
            }
        }

        public static float ToSigned180(float x)
        {
            return Mathf.DeltaAngle(0f, x);
        }
    }

    [Serializable]
    public sealed class ToggleGroup
    {
        [Header("対象の Collider 群（空でも可）")]
        public Collider[] Colliders;

        [Header("対象の GameObject 群（空でも可）")]
        public GameObject[] Objects;
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
        public FloatRange X;
        public FloatRange Y;
        public FloatRange Z;

        [Header("判定誤差（±）")]
        [Min(0f)]
        public float Epsilon = 0.001f;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            Vector3 p = Space ? Space.InverseTransformPoint(t.position) : t.position;

            if (debug)
            {
                Debug.Log(
                    $"[CM] Pos {(Space ? $"Local:{Space.name}" : "World")} " +
                    $"= ({p.x:F4},{p.y:F4},{p.z:F4})  " +
                    $"X[{X.Min},{X.Max}]({X.Enabled}) " +
                    $"Y[{Y.Min},{Y.Max}]({Y.Enabled}) " +
                    $"Z[{Z.Min},{Z.Max}]({Z.Enabled})  ±{Epsilon}"
                );
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
        public AngleRange X;
        public AngleRange Y;
        public AngleRange Z;

        public bool IsSatisfied(Transform t, bool debug)
        {
            if (!Enabled) return true;
            if (!(X.IsActive || Y.IsActive || Z.IsActive)) return false;

            // Unityは0..360表現なので -180..180 に整える
            Vector3 eRaw = UseLocalEuler ? t.localEulerAngles : t.eulerAngles;
            Vector3 e = new Vector3(
                AngleRange.ToSigned180(eRaw.x),
                AngleRange.ToSigned180(eRaw.y),
                AngleRange.ToSigned180(eRaw.z)
            );

            if (debug)
            {
                Debug.Log(
                    $"[CM] Euler {(UseLocalEuler ? "Local" : "World")} (signed)=({e.x:F1},{e.y:F1},{e.z:F1})  " +
                    $"XR[{X.Min},{X.Max}]({X.Enabled}) " +
                    $"YR[{Y.Min},{Y.Max}]({Y.Enabled}) " +
                    $"ZR[{Z.Min},{Z.Max}]({Z.Enabled})"
                );
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
        [Tooltip("このルールがマッチしたら『有効化したい』もの")]
        public ToggleGroup EnableOnMatch;

        [Tooltip("このルールがマッチしたら『無効化したい』もの")]
        public ToggleGroup DisableOnMatch;

        [Tooltip("このルールがマッチしたら以降のルールを無視（同じ Mirror の中だけで優先勝ち扱い）")]
        public bool StopAfterApply;

        // ApplyDelta はもう使わないが、残しておいても問題なし
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _lastMatched;

        public bool Evaluate(Transform mirror, bool debug)
        {
            if (!(Position.Enabled || Euler.Enabled))
            {
                return false;
            }

            if (!Position.IsSatisfied(mirror, debug))
            {
                return false;
            }

            if (!Euler.IsSatisfied(mirror, debug))
            {
                return false;
            }

            return true;
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

    //============================
    // 追加: デフォルト状態の記録
    //============================

    // 各 Collider / GameObject の「デフォルト状態（何もマッチしていないときの状態）」
    private Dictionary<Collider, bool> m_DefaultColliderEnabled
        = new Dictionary<Collider, bool>();

    private Dictionary<GameObject, bool> m_DefaultObjectActive
        = new Dictionary<GameObject, bool>();

    private void Awake()
    {
        // すべてのルールから対象を拾い、初期状態（enabled / activeSelf）を保存する
        CacheDefaultStates();
    }

    private void CacheDefaultStates()
    {
        m_DefaultColliderEnabled.Clear();
        m_DefaultObjectActive.Clear();

        if (m_Mirrors == null)
        {
            return;
        }

        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            MirrorEntry entry = m_Mirrors[i];
            if (entry == null || entry.Rules == null)
            {
                continue;
            }

            foreach (Rule rule in entry.Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                CacheToggleGroup(rule.EnableOnMatch);
                CacheToggleGroup(rule.DisableOnMatch);
            }
        }
    }

    private void CacheToggleGroup(ToggleGroup group)
    {
        if (group == null)
        {
            return;
        }

        if (group.Colliders != null)
        {
            for (int i = 0; i < group.Colliders.Length; ++i)
            {
                Collider c = group.Colliders[i];
                if (!c)
                {
                    continue;
                }
                if (!m_DefaultColliderEnabled.ContainsKey(c))
                {
                    m_DefaultColliderEnabled[c] = c.enabled;
                }
            }
        }

        if (group.Objects != null)
        {
            for (int i = 0; i < group.Objects.Length; ++i)
            {
                GameObject go = group.Objects[i];
                if (!go)
                {
                    continue;
                }
                if (!m_DefaultObjectActive.ContainsKey(go))
                {
                    m_DefaultObjectActive[go] = go.activeSelf;
                }
            }
        }
    }

    private void Update()
    {
        if (m_Mirrors == null)
        {
            return;
        }

        // 1) 全ルールを評価して「どのルールが今フレームでマッチしているか」を集める
        List<Rule> matchedRules = new List<Rule>();

        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            MirrorEntry entry = m_Mirrors[i];
            if (entry == null || entry.Mirror == null || entry.Rules == null)
            {
                continue;
            }

            Rule[] rules = entry.Rules;
            int n = rules.Length;
            if (n == 0)
            {
                continue;
            }

            bool[] matched = new bool[n];
            int firstStopIndex = -1;

            for (int r = 0; r < n; ++r)
            {
                Rule rule = rules[r];
                if (rule == null)
                {
                    continue;
                }

                bool debug = (m_Mirrors.Length == 1 && entry.Rules.Length == 1 && m_DebugLog);
                bool isMatch = rule.Evaluate(entry.Mirror, debug);
                matched[r] = isMatch;

                if (firstStopIndex < 0 && isMatch && rule.StopAfterApply)
                {
                    firstStopIndex = r;
                }
            }

            if (firstStopIndex >= 0)
            {
                // StopAfterApply が立っていたら、それ以降のルールは強制的に「不成立」にする
                for (int r = firstStopIndex + 1; r < n; ++r)
                {
                    matched[r] = false;
                }
            }

            // このMirrorでマッチしたルールだけ記録
            for (int r = 0; r < n; ++r)
            {
                if (matched[r] && rules[r] != null)
                {
                    matchedRules.Add(rules[r]);
                }
            }
        }

        // 2) 今フレームの「最終的なON/OFF」を決めるための辞書を準備する
        //    まずデフォルト状態で初期化する
        Dictionary<Collider, bool> desiredColliderEnabled = new Dictionary<Collider, bool>(m_DefaultColliderEnabled);
        Dictionary<GameObject, bool> desiredObjectActive = new Dictionary<GameObject, bool>(m_DefaultObjectActive);

        // 3) マッチしたルールを使って「有効化したいもの」を反映
        //    ※ 後のステップで「無効化したいもの」を上書きするので、
        //       最終的には Disable が Enable より優先される
        for (int i = 0; i < matchedRules.Count; ++i)
        {
            Rule rule = matchedRules[i];
            if (rule == null)
            {
                continue;
            }

            ToggleGroup eg = rule.EnableOnMatch;
            if (eg != null)
            {
                if (eg.Colliders != null)
                {
                    for (int c = 0; c < eg.Colliders.Length; ++c)
                    {
                        Collider col = eg.Colliders[c];
                        if (!col) continue;
                        desiredColliderEnabled[col] = true;
                    }
                }

                if (eg.Objects != null)
                {
                    for (int o = 0; o < eg.Objects.Length; ++o)
                    {
                        GameObject go = eg.Objects[o];
                        if (!go) continue;
                        desiredObjectActive[go] = true;
                    }
                }
            }
        }

        // 4) マッチしたルールを使って「無効化したいもの」を反映（←これが最優先）
        for (int i = 0; i < matchedRules.Count; ++i)
        {
            Rule rule = matchedRules[i];
            if (rule == null)
            {
                continue;
            }

            ToggleGroup dg = rule.DisableOnMatch;
            if (dg != null)
            {
                if (dg.Colliders != null)
                {
                    for (int c = 0; c < dg.Colliders.Length; ++c)
                    {
                        Collider col = dg.Colliders[c];
                        if (!col) continue;
                        desiredColliderEnabled[col] = false;
                    }
                }

                if (dg.Objects != null)
                {
                    for (int o = 0; o < dg.Objects.Length; ++o)
                    {
                        GameObject go = dg.Objects[o];
                        if (!go) continue;
                        desiredObjectActive[go] = false;
                    }
                }
            }
        }

        // 5) まとめて適用（今フレームの最終状態を一括で反映）
        foreach (var kv in desiredColliderEnabled)
        {
            Collider col = kv.Key;
            if (!col) continue;
            bool wantEnabled = kv.Value;
            if (col.enabled != wantEnabled)
            {
                col.enabled = wantEnabled;
            }
        }

        foreach (var kv in desiredObjectActive)
        {
            GameObject go = kv.Key;
            if (!go) continue;
            bool wantActive = kv.Value;
            if (go.activeSelf != wantActive)
            {
                go.SetActive(wantActive);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (m_Mirrors == null)
        {
            return;
        }

        foreach (var entry in m_Mirrors)
        {
            if (entry == null || entry.Rules == null)
            {
                continue;
            }

            foreach (var rule in entry.Rules)
            {
                if (rule == null || !rule.Position.Enabled)
                {
                    continue;
                }

                var pos = rule.Position;

                float xMin = pos.X.Enabled ? Mathf.Min(pos.X.Min, pos.X.Max) : 0f;
                float xMax = pos.X.Enabled ? Mathf.Max(pos.X.Min, pos.X.Max) : 0f;
                float yMin = pos.Y.Enabled ? Mathf.Min(pos.Y.Min, pos.Y.Max) : 0f;
                float yMax = pos.Y.Enabled ? Mathf.Max(pos.Y.Min, pos.Y.Max) : 0f;
                float zMin = pos.Z.Enabled ? Mathf.Min(pos.Z.Min, pos.Z.Max) : 0f;
                float zMax = pos.Z.Enabled ? Mathf.Max(pos.Z.Min, pos.Z.Max) : 0f;

                Vector3 center = new Vector3(
                    (xMin + xMax) * 0.5f,
                    (yMin + yMax) * 0.5f,
                    (zMin + zMax) * 0.5f
                );

                Vector3 size = new Vector3(
                    Mathf.Abs(xMax - xMin),
                    Mathf.Abs(yMax - yMin),
                    Mathf.Abs(zMax - zMin)
                );

                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = pos.Space
                    ? pos.Space.localToWorldMatrix
                    : Matrix4x4.identity;

                Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(center, size);

                Gizmos.matrix = prev;
            }
        }
    }
#endif
}
