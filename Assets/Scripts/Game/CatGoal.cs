using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class CatGoal : MonoBehaviour
{
    public enum RangeShape { Sphere, Box }

    [Header("=== 対象 ===")]
    [Tooltip("範囲判定の基準（Base）。未設定ならこのコンポーネントの Transform")]
    [SerializeField] private Transform m_Base;

    [Tooltip("範囲の中心となる Target（ゴールのモデルなど）")]
    [SerializeField] private Transform m_Target;

    [Header("=== 範囲設定 ===")]
    [SerializeField] private RangeShape m_Shape = RangeShape.Sphere;

    [Tooltip("Shape = Sphere の半径")]
    [SerializeField] private float m_SphereRadius = 3f;

    [Tooltip("Shape = Box の半サイズ（X/Y/Z）")]
    [SerializeField] private Vector3 m_BoxHalfExtents = new Vector3(1f, 1f, 1f);

    [Tooltip("Box を Target の回転に追従させる（true: OBB / false: ワールド軸AABB）")]
    [SerializeField] private bool m_BoxUseTargetRotation = true;

    [Header("=== 表示・サウンド ===")]
    [Tooltip("範囲に入ったら表示する RawImage")]
    [SerializeField] private RawImage m_RawImageToShow;

    [Tooltip("範囲に入った瞬間に鳴らす SE")]
    [SerializeField] private AudioClip m_SeOnEnter;

    [Tooltip("SE 再生に使う AudioSource（未設定なら自身から取得）")]
    [SerializeField] private AudioSource m_AudioSource;

    [Header("=== シーン遷移 ===")]
    [Tooltip("Enter / KeypadEnter / Space / Escape 押下時に遷移するシーン名（Build Settings に登録必須）")]
    [SerializeField] private string m_SceneName;

    [Tooltip("“範囲内のときのみ”遷移キーを受け付ける")]
    [SerializeField] private bool m_RequireInRangeToTransition = true;

    [Tooltip("範囲から出たら RawImage を自動で隠す")]
    [SerializeField] private bool m_AutoHideOnExit = true;

    [Header("=== ゴール時の制御 ===")]
    [Tooltip("範囲に入った瞬間、Base の SpriteMover を LockMovement() で停止")]
    [SerializeField] private bool m_LockMoverOnEnter = true;

    [Tooltip("LockMovement に渡す hideVisual（表示も隠す）")]
    [SerializeField] private bool m_LockHideVisual = true;

    [Tooltip("範囲に入った瞬間に Time.timeScale=0 にして“世界”を停止（UI は unscaled で動かす想定）")]
    [SerializeField] private bool m_PauseTimeOnEnter = false;

    [Tooltip("Pause した場合、シーン遷移の直前に必ず 1.0 に戻す")]
    [SerializeField] private bool m_UnpauseOnTransition = true;

    [Tooltip("Pause したままこのオブジェクトが破棄/無効化された場合に備えて自動復帰")]
    [SerializeField] private bool m_UnpauseOnDisableOrDestroy = true;

    [Header("=== イベント（任意） ===")]
    public UnityEvent OnGoalEnter;     // 範囲に入った瞬間
    public UnityEvent OnGoalExit;      // 範囲から出た瞬間
    public UnityEvent OnBeforeLoad;    // シーン遷移直前
    public UnityEvent OnAfterEnterUI;  // UI 表示直後（SE再生後）

    // ランタイム
    private bool m_WasInRange;
    private bool m_DidPauseTime; // このコンポーネントが timeScale を 0 にしたかどうか

    private void Awake()
    {
        if (m_Base == null) m_Base = transform;
        if (m_AudioSource == null) m_AudioSource = GetComponent<AudioSource>();

        if (m_RawImageToShow != null)
        {
            m_RawImageToShow.enabled = false; // 起動時は非表示
        }
        m_WasInRange = false;
        m_DidPauseTime = false;
    }

    private void OnDisable()
    {
        // 保険：自分が止めた timeScale は自分で戻す
        if (m_UnpauseOnDisableOrDestroy && m_DidPauseTime)
        {
            Time.timeScale = 1f;
            m_DidPauseTime = false;
        }
    }

    private void OnDestroy()
    {
        if (m_UnpauseOnDisableOrDestroy && m_DidPauseTime)
        {
            Time.timeScale = 1f;
            m_DidPauseTime = false;
        }
    }

    private void Update()
    {
        bool inRange = IsInRange();

        // === エッジ：Enter ===
        if (inRange && !m_WasInRange)
        {
            if (m_RawImageToShow != null) m_RawImageToShow.enabled = true;
            PlayOneShotSafe(m_SeOnEnter);

            // SpriteMover を停止
            if (m_LockMoverOnEnter && m_Base != null)
            {
                var mover = m_Base.GetComponent<SpriteMover>();
                if (mover == null)
                {
                    // Base 直下に無い場合は子からも探す（任意だが便利）
                    mover = m_Base.GetComponentInChildren<SpriteMover>(true);
                }
                if (mover != null)
                {
                    mover.LockMovement(m_LockHideVisual);
                }
            }

            // World を停止
            if (m_PauseTimeOnEnter)
            {
                Time.timeScale = 0f;
                m_DidPauseTime = true;
            }

            OnGoalEnter?.Invoke();
            OnAfterEnterUI?.Invoke();
        }
        // === エッジ：Exit ===
        else if (!inRange && m_WasInRange)
        {
            if (m_AutoHideOnExit && m_RawImageToShow != null) m_RawImageToShow.enabled = false;
            OnGoalExit?.Invoke();
        }

        m_WasInRange = inRange;

        // === シーン遷移キー ===
        bool pressed =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Escape);

        if (pressed && (!m_RequireInRangeToTransition || inRange))
        {
            if (!string.IsNullOrEmpty(m_SceneName))
            {
                OnBeforeLoad?.Invoke();

                if (m_DidPauseTime && m_UnpauseOnTransition)
                {
                    Time.timeScale = 1f; // 必ず復帰
                    m_DidPauseTime = false;
                }

                SceneManager.LoadScene(m_SceneName);
            }
        }
    }

    private bool IsInRange()
    {
        if (m_Base == null || m_Target == null) return false;

        Vector3 p = m_Base.position;

        if (m_Shape == RangeShape.Sphere)
        {
            float r = Mathf.Max(0f, m_SphereRadius);
            return (p - m_Target.position).sqrMagnitude <= r * r;
        }
        else
        {
            Vector3 half = new Vector3(
                Mathf.Abs(m_BoxHalfExtents.x),
                Mathf.Abs(m_BoxHalfExtents.y),
                Mathf.Abs(m_BoxHalfExtents.z)
            );

            if (m_BoxUseTargetRotation)
            {
                // OBB：Target ローカル空間で AABB 判定
                Vector3 local = m_Target.InverseTransformPoint(p);
                return Mathf.Abs(local.x) <= half.x &&
                       Mathf.Abs(local.y) <= half.y &&
                       Mathf.Abs(local.z) <= half.z;
            }
            else
            {
                // AABB：ワールド軸アライン
                Vector3 delta = p - m_Target.position;
                return Mathf.Abs(delta.x) <= half.x &&
                       Mathf.Abs(delta.y) <= half.y &&
                       Mathf.Abs(delta.z) <= half.z;
            }
        }
    }

    private void PlayOneShotSafe(AudioClip clip)
    {
        if (clip == null) return;

        if (m_AudioSource != null)
        {
            m_AudioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, (m_Base != null ? m_Base.position : transform.position));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (m_Target == null) return;

        Gizmos.color = Color.cyan;

        if (m_Shape == RangeShape.Sphere)
        {
            Gizmos.DrawWireSphere(m_Target.position, Mathf.Max(0f, m_SphereRadius));
        }
        else
        {
            Vector3 half = new Vector3(Mathf.Abs(m_BoxHalfExtents.x), Mathf.Abs(m_BoxHalfExtents.y), Mathf.Abs(m_BoxHalfExtents.z));
            if (m_BoxUseTargetRotation)
            {
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(m_Target.position, m_Target.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, half * 2f);
                Gizmos.matrix = old;
            }
            else
            {
                Gizmos.DrawWireCube(m_Target.position, half * 2f);
            }
        }
    }
#endif
}
