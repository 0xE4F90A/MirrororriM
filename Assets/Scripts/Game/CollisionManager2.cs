using System;
using System.Collections.Generic;
//using UnityEditor.SceneManagement;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CollisionManager2 : MonoBehaviour
{
    // 対象をまとめてOFFにする用途だけなので、EnableOnMatch は削除済み

    [Serializable]
    public sealed class ToggleGroup
    {
        [Header("このルールがマッチしたら OFF にする Collider 群")]
        public Collider[] Colliders;

        [Header("このルールがマッチしたら OFF にする GameObject 群")]
        public GameObject[] Objects;
    }

    [Serializable]
    public struct StageCondition
    {
        [Header("このステージ状態に一致したらマッチとみなす")]
        public StageId Stage;

        [Header("位置の許容差[m]（各軸ごとに |actual-target| <= これ で判定）")]
        [Min(0f)]
        public float PositionEpsilon;

        [Header("回転の許容差[deg]（各軸ごとに DeltaAngle <= これ で判定）")]
        [Min(0f)]
        public float AngleEpsilonDeg;
    }

    [Serializable]
    public sealed class Rule
    {
        [Header("=== OR条件群（どれか1つ成立でこのルール成立） ===")]
        [Tooltip("配列内のいずれかのステージ状態に入っていれば、このルールは成立扱い")]
        public StageCondition[] Stages;

        [Header("=== 成立時の操作 ===")]
        [Tooltip("マッチ中はこれらを OFF にする")]
        public ToggleGroup DisableOnMatch;

        [Tooltip("このルールがマッチしたら以降のルールを無視（この Mirror 内での優先勝ち）")]
        public bool StopAfterApply;

        public bool Evaluate(Transform mirror, bool debugLog)
        {
            if (Stages == null || Stages.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < Stages.Length; ++i)
            {
                if (IsStageMatch(mirror, Stages[i], debugLog))
                {
                    return true; // OR条件なのでどれか1つでOK
                }
            }
            return false;
        }

        private static bool IsStageMatch(Transform mirror, in StageCondition cond, bool debug)
        {
            // STAGE_STATE から理想位置/回転を引く
            if (!STAGE_STATE.TryGet(cond.Stage, out Vector3 targetPos, out Vector3 targetEulerDeg))
            {
                return false;
            }

            // --- 位置判定 ---
            Vector3 nowPos = mirror.position;
            bool posOK =
                Mathf.Abs(nowPos.x - targetPos.x) <= cond.PositionEpsilon &&
                Mathf.Abs(nowPos.y - targetPos.y) <= cond.PositionEpsilon &&
                Mathf.Abs(nowPos.z - targetPos.z) <= cond.PositionEpsilon;

            // --- 回転判定（-180..180で各軸比較） ---
            Vector3 nowEulerRaw = mirror.eulerAngles;
            Vector3 nowEulerSigned = new Vector3(
                Mathf.DeltaAngle(0f, nowEulerRaw.x),
                Mathf.DeltaAngle(0f, nowEulerRaw.y),
                Mathf.DeltaAngle(0f, nowEulerRaw.z)
            );

            Vector3 targetEulerSigned = new Vector3(
                Mathf.DeltaAngle(0f, targetEulerDeg.x),
                Mathf.DeltaAngle(0f, targetEulerDeg.y),
                Mathf.DeltaAngle(0f, targetEulerDeg.z)
            );

            bool rotOK =
                Mathf.Abs(Mathf.DeltaAngle(nowEulerSigned.x, targetEulerSigned.x)) <= cond.AngleEpsilonDeg &&
                Mathf.Abs(Mathf.DeltaAngle(nowEulerSigned.y, targetEulerSigned.y)) <= cond.AngleEpsilonDeg &&
                Mathf.Abs(Mathf.DeltaAngle(nowEulerSigned.z, targetEulerSigned.z)) <= cond.AngleEpsilonDeg;

            if (debug)
            {
                Debug.Log(
                    $"[CollisionManager2] Stage={cond.Stage} " +
                    $"posNow={nowPos:F3} posTgt={targetPos:F3} posOK={posOK} eps={cond.PositionEpsilon} / " +
                    $"rotNow=({nowEulerSigned.x:F1},{nowEulerSigned.y:F1},{nowEulerSigned.z:F1}) " +
                    $"rotTgt=({targetEulerSigned.x:F1},{targetEulerSigned.y:F1},{targetEulerSigned.z:F1}) " +
                    $"rotOK={rotOK} epsDeg={cond.AngleEpsilonDeg}"
                );
            }

            return posOK && rotOK;
        }
    }

    [Serializable]
    public sealed class MirrorEntry
    {
        [Header("監視対象（プレイヤー等）")]
        public Transform Mirror;

        [Header("この Mirror に対するルール一覧（上から評価）")]
        public Rule[] Rules;
    }

    [Header("監視する Mirror の一覧")]
    [SerializeField] private MirrorEntry[] m_Mirrors;

    [Header("デバッグログを出す（Mirror1+Rule1だけログ）")]
    [SerializeField] private bool m_DebugLog = false;

    // --- 内部：初期状態キャッシュ（マッチしてない時の状態に戻す用） ---
    private readonly Dictionary<Collider, bool> m_DefaultColliderEnabled = new Dictionary<Collider, bool>();
    private readonly Dictionary<GameObject, bool> m_DefaultObjectActive = new Dictionary<GameObject, bool>();

    private void Awake()
    {
        CacheDefaultStates();
    }

    private void CacheDefaultStates()
    {
        m_DefaultColliderEnabled.Clear();
        m_DefaultObjectActive.Clear();

        if (m_Mirrors == null) return;

        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            MirrorEntry entry = m_Mirrors[i];
            if (entry == null || entry.Rules == null) continue;

            foreach (var rule in entry.Rules)
            {
                if (rule == null) continue;
                CacheToggleGroup(rule.DisableOnMatch);
            }
        }
    }

    private void CacheToggleGroup(ToggleGroup group)
    {
        if (group == null) return;

        if (group.Colliders != null)
        {
            for (int i = 0; i < group.Colliders.Length; ++i)
            {
                Collider c = group.Colliders[i];
                if (!c) continue;
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
                if (!go) continue;
                if (!m_DefaultObjectActive.ContainsKey(go))
                {
                    m_DefaultObjectActive[go] = go.activeSelf;
                }
            }
        }
    }

    private void Update()
    {
        if (m_Mirrors == null) return;

        // 1) 今フレームでマッチしたルールを集める
        List<Rule> matchedRules = new List<Rule>();

        for (int i = 0; i < m_Mirrors.Length; ++i)
        {
            MirrorEntry entry = m_Mirrors[i];
            if (entry == null || entry.Mirror == null || entry.Rules == null) continue;

            var rules = entry.Rules;
            int n = rules.Length;
            if (n == 0) continue;

            bool[] matched = new bool[n];
            int firstStopIndex = -1;

            for (int r = 0; r < n; ++r)
            {
                Rule rule = rules[r];
                if (rule == null) continue;

                bool debugThisRule =
                    (m_Mirrors.Length == 1) &&
                    (entry.Rules.Length == 1) &&
                    m_DebugLog;

                bool isMatch = rule.Evaluate(entry.Mirror, debugThisRule);
                matched[r] = isMatch;

                if (firstStopIndex < 0 && isMatch && rule.StopAfterApply)
                {
                    firstStopIndex = r;
                }
            }

            if (firstStopIndex >= 0)
            {
                // 優先ルールが当たったら、その後ろは評価結果を無効化
                for (int r = firstStopIndex + 1; r < n; ++r)
                {
                    matched[r] = false;
                }
            }

            for (int r = 0; r < n; ++r)
            {
                if (matched[r] && rules[r] != null)
                {
                    matchedRules.Add(rules[r]);
                }
            }
        }

        // 2) デフォルト状態をベースに望ましい最終状態を用意
        Dictionary<Collider, bool> desiredColliderEnabled = new Dictionary<Collider, bool>(m_DefaultColliderEnabled);
        Dictionary<GameObject, bool> desiredObjectActive = new Dictionary<GameObject, bool>(m_DefaultObjectActive);

        // 3) マッチしたルールはその対象をOFFにする
        for (int i = 0; i < matchedRules.Count; ++i)
        {
            Rule rule = matchedRules[i];
            if (rule == null) continue;

            ToggleGroup dg = rule.DisableOnMatch;
            if (dg == null) continue;

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

        // 4) 実オブジェクトに反映
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
        if (m_Mirrors == null) return;

        Gizmos.color = Color.cyan;

        foreach (var entry in m_Mirrors)
        {
            if (entry == null || entry.Rules == null) continue;

            foreach (var rule in entry.Rules)
            {
                if (rule == null || rule.Stages == null) continue;

                foreach (var st in rule.Stages)
                {
                    if (!STAGE_STATE.TryGet(st.Stage, out Vector3 pos, out Vector3 euler)) continue;

                    // 位置マーカー
                    Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
                    Gizmos.DrawSphere(pos, 0.08f);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(pos, 0.08f);

                    // 前方向の矢印をちょっとだけ
                    Quaternion q = Quaternion.Euler(euler);
                    Vector3 dir = q * Vector3.forward;
                    Gizmos.DrawLine(pos, pos + dir * 0.4f);
                }
            }
        }
    }
#endif
}
