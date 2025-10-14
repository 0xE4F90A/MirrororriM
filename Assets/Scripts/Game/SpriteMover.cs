using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class SpriteMover : MonoBehaviour
{
    //=== 表示に使う子オブジェクト（反転なし・全割り当て） ==========
    [Header("=== 表示: 押下中 ===")]
    [SerializeField] private GameObject m_PressRight;
    [SerializeField] private GameObject m_PressLeft;
    [SerializeField] private GameObject m_PressUp;
    [SerializeField] private GameObject m_PressDown;

    [Header("=== 表示: キー離し直後 ===")]
    [SerializeField] private GameObject m_ReleaseRight;
    [SerializeField] private GameObject m_ReleaseLeft;
    [SerializeField] private GameObject m_ReleaseUp;
    [SerializeField] private GameObject m_ReleaseDown;

    [Header("起動時に表示するオブジェクト（未指定なら全非表示）")]
    [SerializeField] private GameObject m_DefaultVisual;
    //=====================================================

    [Header("移動向き(左キーか上キー)")]
    [SerializeField] private bool m_IsKeyLeft = false;

    [Header("移動設定")]
    [SerializeField] private float m_MoveSpeed = 2.0f;
    [SerializeField, Tooltip("true: XZ 平面で移動（3D用途） / false: XY 平面で移動（2D用途）")]
    private bool m_MoveInXZPlane = true;

    [Header("物理設定")]
    [SerializeField, Tooltip("重力を使わない場合は false 推奨（上から見下ろしなど）")]
    private bool m_UseGravity = false;
    [SerializeField, Tooltip("XZ移動時にYを固定")]
    private bool m_FreezeYPosition = true;

    // === LOCK: 追加（移動系停止制御） ==========================
    [Header("=== 移動ロック ===")]
    [SerializeField, Tooltip("true にすると移動系（入力/物理）を完全停止")]
    private bool m_MovementLocked = false;

    [SerializeField, Tooltip("ロック時に表示（子オブジェクト）も全て非表示にする")]
    private bool m_HideVisualWhenLocked = true;

    [SerializeField, Tooltip("ロック時、Rigidbody の位置を全軸フリーズする（解除で元に戻す）")]
    private bool m_FreezeAllPositionOnLock = true;

    /// <summary>外部から移動ロックを有効化</summary>
    public void LockMovement(bool hideVisual = true)
    {
        m_MovementLocked = true;
        m_HideVisualWhenLocked = hideVisual;
        ApplyLockStateImmediate();
    }

    /// <summary>外部から移動ロックを解除</summary>
    public void UnlockMovement()
    {
        m_MovementLocked = false;
        RestoreConstraints();
        // 表示は以降の入力に従って更新される
    }
    // ==========================================================

    private enum Dir { None, Right, Left, Up, Down }

    private Dir m_LastPressed = Dir.None;

    // 物理移動用
    private Rigidbody m_Rigidbody;
    private Vector3 m_MoveDir3D = Vector3.zero; // Updateで入力を読み、FixedUpdateで使用

    // === LOCK: 追加（元の拘束値の保持） =========================
    private RigidbodyConstraints m_OriginalConstraints;
    // ===========================================================

    private void Awake()
    {
        // 初期表示
        if (m_DefaultVisual != null) ShowOnly(m_DefaultVisual);
        else HideAllChildren();

        // Rigidbody 準備
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null)
        {
            m_Rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        m_Rigidbody.useGravity = m_UseGravity;
        m_Rigidbody.isKinematic = false; // MovePositionで衝突を止めるには非キネマティックにする
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation; // 物理で倒れないように

        if (m_MoveInXZPlane && m_FreezeYPosition)
        {
            m_Rigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
        }

        // === LOCK: 追加（元拘束保存 & 起動時ロック反映） ===
        m_OriginalConstraints = m_Rigidbody.constraints;
        if (m_MovementLocked)
        {
            ApplyLockStateImmediate();
        }
    }

    private void Update()
    {
        // === LOCK: 先頭で監視 ===
        if (m_MovementLocked)
        {
            // 入力を読まない＆移動方向ゼロ
            m_MoveDir3D = Vector3.zero;
            m_LastPressed = Dir.None;

            if (m_HideVisualWhenLocked)
            {
                HideAllChildren();
            }
            return; // 以降の入力/表示処理はスキップ
        }

        // --- 離した瞬間の表示 ---
        if (GetKeyUpRight()) ShowReleaseHorizontal(isLeft: false);
        if (GetKeyUpLeft()) ShowReleaseHorizontal(isLeft: true);
        if (GetKeyUpUp()) ShowReleaseVertical(isDown: false);
        if (GetKeyUpDown()) ShowReleaseVertical(isDown: true);

        // --- 押下開始の記録 ---
        if (GetKeyDownRight()) { m_LastPressed = Dir.Right; }
        if (GetKeyDownLeft()) { m_LastPressed = Dir.Left; }
        if (GetKeyDownUp()) { m_LastPressed = Dir.Up; }
        if (GetKeyDownDown()) { m_LastPressed = Dir.Down; }

        // --- 押下中の表示（最後に押した方向を優先） ---
        if (IsAnyHeld())
        {
            Dir active = GetActiveHeldDir();
            ShowPressed(active);
        }

        // --- 入力→移動方向ベクトルを計算（物理は FixedUpdate で適用） ---
        m_MoveDir3D = ReadMoveDirection3D();
    }

    private void FixedUpdate()
    {
        if (m_Rigidbody == null) return;

        // === LOCK: 物理側も完全停止 ===
        if (m_MovementLocked)
        {
#if UNITY_6000_0_OR_NEWER
            m_Rigidbody.linearVelocity = Vector3.zero;
#else
            m_Rigidbody.velocity = Vector3.zero;
#endif
            m_Rigidbody.angularVelocity = Vector3.zero;
            return;
        }

        // 等速移動（物理）
        if (m_MoveDir3D.sqrMagnitude > 0f)
        {
            Vector3 delta = m_MoveDir3D.normalized * m_MoveSpeed * Time.fixedDeltaTime;
            Vector3 next = m_Rigidbody.position + delta;
            m_Rigidbody.MovePosition(next); // 壁のColliderにぶつかればここで止まる
        }
    }

    // ===== 入力→3D方向変換 =====
    private Vector3 ReadMoveDirection3D()
    {
        int x = 0;
        int y = 0;

        if(m_IsKeyLeft)
        {
            if (GetKeyRight()) y -= 1;
            if (GetKeyLeft()) y += 1;
            if (GetKeyUp()) x += 1;
            if (GetKeyDown()) x -= 1;
        }
        else
        {
            if (GetKeyRight()) x += 1;
            if (GetKeyLeft()) x -= 1;
            if (GetKeyUp()) y += 1;
            if (GetKeyDown()) y -= 1;
        }
        if (x == 0 && y == 0) return Vector3.zero;

        if (m_MoveInXZPlane) return new Vector3(x, 0f, y).normalized;
        else return new Vector3(x, y, 0f).normalized;
    }

    // ===== 入力ヘルパ =====
    private static bool GetKeyRight() => Input.GetKey(KeyCode.D);
    private static bool GetKeyLeft() => Input.GetKey(KeyCode.A);
    private static bool GetKeyUp() => Input.GetKey(KeyCode.W);
    private static bool GetKeyDown() => Input.GetKey(KeyCode.S);

    private static bool GetKeyDownRight() => Input.GetKeyDown(KeyCode.D);
    private static bool GetKeyDownLeft() => Input.GetKeyDown(KeyCode.A);
    private static bool GetKeyDownUp() => Input.GetKeyDown(KeyCode.W);
    private static bool GetKeyDownDown() => Input.GetKeyDown(KeyCode.S);

    private static bool GetKeyUpRight() => Input.GetKeyUp(KeyCode.D);
    private static bool GetKeyUpLeft() => Input.GetKeyUp(KeyCode.A);
    private static bool GetKeyUpUp() => Input.GetKeyUp(KeyCode.W);
    private static bool GetKeyUpDown() => Input.GetKeyUp(KeyCode.S);

    private static bool IsHeldRight() => GetKeyRight();
    private static bool IsHeldLeft() => GetKeyLeft();
    private static bool IsHeldUp() => GetKeyUp();
    private static bool IsHeldDown() => GetKeyDown();

    private static bool IsAnyHeld() => GetKeyRight() || GetKeyLeft() || GetKeyUp() || GetKeyDown();

    private Dir GetActiveHeldDir()
    {
        if (m_LastPressed != Dir.None && IsHeld(m_LastPressed))
            return m_LastPressed;

        if (IsHeldUp()) return Dir.Up;
        if (IsHeldDown()) return Dir.Down;
        if (IsHeldRight()) return Dir.Right;
        if (IsHeldLeft()) return Dir.Left;

        return Dir.None;
    }

    private static bool IsHeld(Dir dir)
    {
        switch (dir)
        {
            case Dir.Right: return IsHeldRight();
            case Dir.Left: return IsHeldLeft();
            case Dir.Up: return IsHeldUp();
            case Dir.Down: return IsHeldDown();
            default: return false;
        }
    }

    // ===== 表示制御（反転なし） =====
    private void ShowPressed(Dir dir)
    {
        switch (dir)
        {
            case Dir.Right: ShowOnly(m_PressRight); break;
            case Dir.Left: ShowOnly(m_PressLeft); break;
            case Dir.Up: ShowOnly(m_PressUp); break;
            case Dir.Down: ShowOnly(m_PressDown); break;
            default: break;
        }
    }

    private void ShowReleaseHorizontal(bool isLeft)
    {
        ShowOnly(isLeft ? m_ReleaseLeft : m_ReleaseRight);
    }

    private void ShowReleaseVertical(bool isDown)
    {
        ShowOnly(isDown ? m_ReleaseDown : m_ReleaseUp);
    }

    private void ShowOnly(GameObject target)
    {
        // すべての表示オブジェクトを列挙して排他的に切替
        if (m_PressRight) m_PressRight.SetActive(target == m_PressRight);
        if (m_PressLeft) m_PressLeft.SetActive(target == m_PressLeft);
        if (m_PressUp) m_PressUp.SetActive(target == m_PressUp);
        if (m_PressDown) m_PressDown.SetActive(target == m_PressDown);

        if (m_ReleaseRight) m_ReleaseRight.SetActive(target == m_ReleaseRight);
        if (m_ReleaseLeft) m_ReleaseLeft.SetActive(target == m_ReleaseLeft);
        if (m_ReleaseUp) m_ReleaseUp.SetActive(target == m_ReleaseUp);
        if (m_ReleaseDown) m_ReleaseDown.SetActive(target == m_ReleaseDown);

        // target==null なら単に全非表示になる
    }

    // === LOCK: 便利関数 ================================
    private void HideAllChildren()
    {
        if (m_PressRight) m_PressRight.SetActive(false);
        if (m_PressLeft) m_PressLeft.SetActive(false);
        if (m_PressUp) m_PressUp.SetActive(false);
        if (m_PressDown) m_PressDown.SetActive(false);

        if (m_ReleaseRight) m_ReleaseRight.SetActive(false);
        if (m_ReleaseLeft) m_ReleaseLeft.SetActive(false);
        if (m_ReleaseUp) m_ReleaseUp.SetActive(false);
        if (m_ReleaseDown) m_ReleaseDown.SetActive(false);
    }

    private void ApplyLockStateImmediate()
    {
        // 入力/表示抑止
        m_MoveDir3D = Vector3.zero;
        m_LastPressed = Dir.None;

        if (m_HideVisualWhenLocked)
            HideAllChildren();

        // 物理停止
        if (m_Rigidbody != null)
        {
#if UNITY_6000_0_OR_NEWER
            m_Rigidbody.linearVelocity = Vector3.zero;
#else
            m_Rigidbody.velocity = Vector3.zero;
#endif
            m_Rigidbody.angularVelocity = Vector3.zero;

            if (m_FreezeAllPositionOnLock)
            {
                m_OriginalConstraints = m_Rigidbody.constraints; // 念のため最新を保存
                m_Rigidbody.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezePositionY |
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotation;
            }
        }
    }

    private void RestoreConstraints()
    {
        if (m_Rigidbody != null)
        {
            m_Rigidbody.constraints = m_OriginalConstraints;
        }
    }
    // ====================================================
}
