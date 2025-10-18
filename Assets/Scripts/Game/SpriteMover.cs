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

    // 直近に押し始めた方向（Held優先の決定に使う）
    private Dir m_LastPressed = Dir.None;

    // 物理移動用
    private Rigidbody m_Rigidbody;
    private Vector3 m_MoveDir3D = Vector3.zero; // Updateで入力を読み、FixedUpdateで適用

    // === LOCK: 元の拘束値の保持 =========================
    private RigidbodyConstraints m_OriginalConstraints;
    // ====================================================

    // --- 前フレームの押下状態（エッジ検出用） ---
    private bool m_PrevHeldRight, m_PrevHeldLeft, m_PrevHeldUp, m_PrevHeldDown;

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
        m_Rigidbody.isKinematic = false;
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        if (m_MoveInXZPlane && m_FreezeYPosition)
        {
            m_Rigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
        }

        m_OriginalConstraints = m_Rigidbody.constraints;

        // 起動時ロック反映
        if (m_MovementLocked)
        {
            ApplyLockStateImmediate();
        }

        // 前フレーム押下状態の初期化
        m_PrevHeldRight = m_PrevHeldLeft = m_PrevHeldUp = m_PrevHeldDown = false;
    }

    private void Update()
    {
        // === LOCK: 先頭で監視 ===
        if (m_MovementLocked)
        {
            m_MoveDir3D = Vector3.zero;
            m_LastPressed = Dir.None;
            if (m_HideVisualWhenLocked) HideAllChildren();
            // 押下状態もリセット
            m_PrevHeldRight = m_PrevHeldLeft = m_PrevHeldUp = m_PrevHeldDown = false;
            return;
        }

        // ---- 今フレームの押下状態（Held）を集約（KB + PadBool）----
        bool heldR = GetKeyRight();
        bool heldL = GetKeyLeft();
        bool heldU = GetKeyUp();
        bool heldD = GetKeyDown();

        // ---- エッジ検出（Down/Up）※GetKeyUp/Down は使わない ----
        bool downR = !m_PrevHeldRight && heldR;
        bool downL = !m_PrevHeldLeft && heldL;
        bool downU = !m_PrevHeldUp && heldU;
        bool downD = !m_PrevHeldDown && heldD;

        bool upR = m_PrevHeldRight && !heldR;
        bool upL = m_PrevHeldLeft && !heldL;
        bool upU = m_PrevHeldUp && !heldU;
        bool upD = m_PrevHeldDown && !heldD;

        // ---- 押下開始の記録（最後に押した方向）----
        if (downR) m_LastPressed = Dir.Right;
        else if (downL) m_LastPressed = Dir.Left;
        else if (downU) m_LastPressed = Dir.Up;
        else if (downD) m_LastPressed = Dir.Down;

        // ---- 離した瞬間の表示（1フレームだけ Release を優先表示）----
        // 直近に押していた方向の離しを最優先。その次に固定順で拾う。
        Dir releaseDir = Dir.None;
        if (m_LastPressed == Dir.Right && upR) releaseDir = Dir.Right;
        else if (m_LastPressed == Dir.Left && upL) releaseDir = Dir.Left;
        else if (m_LastPressed == Dir.Up && upU) releaseDir = Dir.Up;
        else if (m_LastPressed == Dir.Down && upD) releaseDir = Dir.Down;
        else
        {
            if (upR) releaseDir = Dir.Right;
            else if (upL) releaseDir = Dir.Left;
            else if (upU) releaseDir = Dir.Up;
            else if (upD) releaseDir = Dir.Down;
        }

        if (releaseDir != Dir.None)
        {
            // Release 優先（このフレームのみ）
            switch (releaseDir)
            {
                case Dir.Right: ShowOnly(m_ReleaseRight); break;
                case Dir.Left: ShowOnly(m_ReleaseLeft); break;
                case Dir.Up: ShowOnly(m_ReleaseUp); break;
                case Dir.Down: ShowOnly(m_ReleaseDown); break;
            }
        }
        else
        {
            // ---- 押下中の表示（最後に押した方向を優先）----
            if (heldR || heldL || heldU || heldD)
            {
                Dir active = GetActiveHeldDir(heldR, heldL, heldU, heldD);
                ShowPressed(active);
            }
            // 何も押していなければ、何も表示しない（必要ならデフォルトへ戻す）
        }

        // ---- 入力→移動方向ベクトル（物理は FixedUpdate で適用）----
        m_MoveDir3D = ReadMoveDirection3D(heldR, heldL, heldU, heldD);

        // ---- 前フレーム押下状態の更新 ----
        m_PrevHeldRight = heldR;
        m_PrevHeldLeft = heldL;
        m_PrevHeldUp = heldU;
        m_PrevHeldDown = heldD;
    }

    private void FixedUpdate()
    {
        if (m_Rigidbody == null) return;

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

        if (m_MoveDir3D.sqrMagnitude > 0f)
        {
            Vector3 delta = m_MoveDir3D.normalized * m_MoveSpeed * Time.fixedDeltaTime;
            Vector3 next = m_Rigidbody.position + delta;
            m_Rigidbody.MovePosition(next);
        }
    }

    // ===== 入力→3D方向変換 =====
    private Vector3 ReadMoveDirection3D(bool heldR, bool heldL, bool heldU, bool heldD)
    {
        int x = 0, y = 0;

        if (m_IsKeyLeft)
        {
            if (heldR) y -= 1;
            if (heldL) y += 1;
            if (heldU) x += 1;
            if (heldD) x -= 1;
        }
        else
        {
            if (heldR) x += 1;
            if (heldL) x -= 1;
            if (heldU) y += 1;
            if (heldD) y -= 1;
        }

        if (x == 0 && y == 0) return Vector3.zero;
        return m_MoveInXZPlane ? new Vector3(x, 0f, y).normalized
                               : new Vector3(x, y, 0f).normalized;
    }

    // ===== 入力ヘルパ（Held 判定のみ） =====
    // ここは KB + PadBool の統合。「押しているか？」だけを見る。
    private static bool GetKeyRight() => Input.GetKey(KeyCode.D) || PadBool.IsRightHeld(PadBool.DirInputSource.LStick);
    private static bool GetKeyLeft() => Input.GetKey(KeyCode.A) || PadBool.IsLeftHeld(PadBool.DirInputSource.LStick);
    private static bool GetKeyUp() => Input.GetKey(KeyCode.W) || PadBool.IsUpHeld(PadBool.DirInputSource.LStick);
    private static bool GetKeyDown() => Input.GetKey(KeyCode.S) || PadBool.IsDownHeld(PadBool.DirInputSource.LStick);

    private Dir GetActiveHeldDir(bool heldR, bool heldL, bool heldU, bool heldD)
    {
        // 直近に押し始めた方向がまだ Held ならそれを優先
        switch (m_LastPressed)
        {
            case Dir.Right: if (heldR) return Dir.Right; break;
            case Dir.Left: if (heldL) return Dir.Left; break;
            case Dir.Up: if (heldU) return Dir.Up; break;
            case Dir.Down: if (heldD) return Dir.Down; break;
        }

        // どれも不成立なら固定優先度（必要に応じて並び替え可）
        if (heldU) return Dir.Up;
        if (heldD) return Dir.Down;
        if (heldR) return Dir.Right;
        if (heldL) return Dir.Left;
        return Dir.None;

        // ローカル関数用
        //bool heldUp   => heldU;
        //bool heldDown => heldD;
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

    private void ShowOnly(GameObject target)
    {
        if (m_PressRight) m_PressRight.SetActive(target == m_PressRight);
        if (m_PressLeft) m_PressLeft.SetActive(target == m_PressLeft);
        if (m_PressUp) m_PressUp.SetActive(target == m_PressUp);
        if (m_PressDown) m_PressDown.SetActive(target == m_PressDown);

        if (m_ReleaseRight) m_ReleaseRight.SetActive(target == m_ReleaseRight);
        if (m_ReleaseLeft) m_ReleaseLeft.SetActive(target == m_ReleaseLeft);
        if (m_ReleaseUp) m_ReleaseUp.SetActive(target == m_ReleaseUp);
        if (m_ReleaseDown) m_ReleaseDown.SetActive(target == m_ReleaseDown);
        // target == null なら全非表示
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
        m_MoveDir3D = Vector3.zero;
        m_LastPressed = Dir.None;
        if (m_HideVisualWhenLocked) HideAllChildren();

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
                m_OriginalConstraints = m_Rigidbody.constraints;
                m_Rigidbody.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezePositionY |
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotation;
            }
        }

        // 押下状態もリセット
        m_PrevHeldRight = m_PrevHeldLeft = m_PrevHeldUp = m_PrevHeldDown = false;
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
