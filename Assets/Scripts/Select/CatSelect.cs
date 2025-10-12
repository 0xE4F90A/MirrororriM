using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class CatSelect : MonoBehaviour
{
    [Header("表示に使う4つの子オブジェクト（インスペクターで割当）")]
    [SerializeField] private GameObject m_Child1; // 右：押下中／上：押下中(反転)
    [SerializeField] private GameObject m_Child2; // 左：押下中／下：押下中(反転)
    [SerializeField] private GameObject m_Child3; // 右/左 のキーを離した瞬間の表示（左は反転）
    [SerializeField] private GameObject m_Child4; // 上/下 のキーを離した瞬間の表示（下は反転）

    [Header("移動設定")]
    [SerializeField] private float m_MoveSpeed = 2.0f;
    [SerializeField, Tooltip("true: XZ 平面で移動（3D用途） / false: XY 平面で移動（2D用途）")]
    private bool m_MoveInXZPlane = true;

    [SerializeField, Tooltip("XZ移動時にYを固定")]
    private bool m_FreezeYPosition = true;

    private enum Dir
    {
        None,
        Right,
        Left,
        Up,
        Down
    }

    private Vector3 m_InitScale1 = Vector3.one;
    private Vector3 m_InitScale2 = Vector3.one;
    private Vector3 m_InitScale3 = Vector3.one;
    private Vector3 m_InitScale4 = Vector3.one;

    private Dir m_LastPressed = Dir.None;

    // 物理移動用
    private Rigidbody m_Rigidbody;
    private Vector3 m_MoveDir3D = Vector3.zero; // Updateで入力を読み、FixedUpdateで使用

    private void Awake()
    {
        if (m_Child1 != null) m_InitScale1 = m_Child1.transform.localScale;
        if (m_Child2 != null) m_InitScale2 = m_Child2.transform.localScale;
        if (m_Child3 != null) m_InitScale3 = m_Child3.transform.localScale;
        if (m_Child4 != null) m_InitScale4 = m_Child4.transform.localScale;

        // 初期表示
        ShowOnly(m_Child3, false);

        // Rigidbody 準備
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null)
        {
            m_Rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        m_Rigidbody.isKinematic = false; // MovePositionで衝突を止めるには非キネマティックにする
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation; // 物理で倒れないように

        if (m_MoveInXZPlane && m_FreezeYPosition)
        {
            m_Rigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
        }
    }

    private void Update()
    {
        // --- 離した瞬間の表示 ---
        if (GetKeyUpRight())
            ShowReleaseHorizontal(isLeft: false); // 右を離した → 3番のみ
        
        if (GetKeyUpLeft())
            ShowReleaseVertical(isDown: false);   // 左を離した → 4番のみ
        
        if (GetKeyUpUp())
            ShowReleaseHorizontal(isLeft: true);  // 上を離した → 3番（反転）
        
        if (GetKeyUpDown())
            ShowReleaseVertical(isDown: true);    // 下を離した → 4番（反転）

        // --- 押下開始の記録 ---
        if (GetKeyDownRight()) { m_LastPressed = Dir.Right; }
        if (GetKeyDownLeft()) { m_LastPressed = Dir.Left; }
        if (GetKeyDownUp()) { m_LastPressed = Dir.Up; }
        if (GetKeyDownDown()) { m_LastPressed = Dir.Down; }

        // --- 押下中の表示（最後に押した方向を優先） ---
        bool anyHeld = IsAnyHeld();
        if (anyHeld)
        {
            Dir active = GetActiveHeldDir();
            ShowPressed(active);
        }

        // --- 入力→移動方向ベクトルを計算（物理は FixedUpdate で適用） ---
        m_MoveDir3D = ReadMoveDirection3D();
    }

    private void FixedUpdate()
    {
        if (m_Rigidbody == null)
            return;
        
        // 等速移動（物理）
        if (m_MoveDir3D.sqrMagnitude > 0f)
        {
            Vector3 delta = m_MoveDir3D.normalized * m_MoveSpeed * Time.fixedDeltaTime;
            Vector3 next = m_Rigidbody.position + (-delta);
            m_Rigidbody.MovePosition(next); // 壁のColliderにぶつかればここで止まる
        }
    }

    // ===== 入力→3D方向変換 =====

    private Vector3 ReadMoveDirection3D()
    {
        // 横移動は従来どおり：RightArrow / LeftArrow / D / A
        int x = 0;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1;

        // 縦移動は Up/Down Arrow のみを参照（W/S は無視して移動させない）
        int y = 0;
        if (Input.GetKey(KeyCode.UpArrow)) y += 1;
        if (Input.GetKey(KeyCode.DownArrow)) y -= 1;

        if (x == 0 && y == 0)
            return Vector3.zero;

        if (m_MoveInXZPlane)
        {
            // XZ 平面：y成分はZへ
            return new Vector3(x, 0f, y).normalized;
        }
        else
        {
            // XY 平面：y成分はYへ
            return new Vector3(x, y, 0f).normalized;
        }
    }


    // ===== 入力ヘルパ =====

    private static bool GetKeyRight()
    {
        return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
    }
    private static bool GetKeyLeft()
    {
        return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
    }
    private static bool GetKeyUp()
    {
        return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
    }
    private static bool GetKeyDown()
    {
        return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
    }

    private static bool GetKeyDownRight()
    {
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    }
    private static bool GetKeyDownLeft()
    {
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    }
    private static bool GetKeyDownUp()
    {
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }
    private static bool GetKeyDownDown()
    {
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool GetKeyUpRight()
    {
        return Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D);
    }
    private static bool GetKeyUpLeft()
    {
        return Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A);
    }
    private static bool GetKeyUpUp()
    {
        return Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W);
    }
    private static bool GetKeyUpDown()
    {
        return Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S);
    }

    private static bool IsHeldRight()
    {
        return GetKeyRight();
    }
    private static bool IsHeldLeft()
    {
        return GetKeyLeft();
    }
    private static bool IsHeldUp()
    {
        return GetKeyUp();
    }
    private static bool IsHeldDown()
    {
        return GetKeyDown();
    }

    private static bool IsAnyHeld()
    {
        return GetKeyRight() || GetKeyLeft() || GetKeyUp() || GetKeyDown();
    }

    private Dir GetActiveHeldDir()
    {
        if (m_LastPressed != Dir.None && IsHeld(m_LastPressed))
        {
            return m_LastPressed;
        }

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

    // ===== 表示制御 =====

    private void ShowPressed(Dir dir)
    {
        switch (dir)
        {
            case Dir.Right:
                ShowOnly(m_Child1, false);
                break;
            case Dir.Left:
                ShowOnly(m_Child2, false);
                break;
            case Dir.Up:
                ShowOnly(m_Child1, true);
                break;
            case Dir.Down:
                ShowOnly(m_Child2, true);
                break;
            default:
                break;
        }
    }

    private void ShowReleaseHorizontal(bool isLeft)
    {
        ShowOnly(m_Child3, isLeft);
    }

    private void ShowReleaseVertical(bool isDown)
    {
        ShowOnly(m_Child4, isDown);
    }

    private void ShowOnly(GameObject target, bool flipX)
    {
        if (m_Child1 != null) m_Child1.SetActive(target == m_Child1);
        if (m_Child2 != null) m_Child2.SetActive(target == m_Child2);
        if (m_Child3 != null) m_Child3.SetActive(target == m_Child3);
        if (m_Child4 != null) m_Child4.SetActive(target == m_Child4);

        if (target == null)
            return;

        if (target == m_Child1)
            ApplyFlip(m_Child1.transform, m_InitScale1, flipX);
        else if (target == m_Child2)
            ApplyFlip(m_Child2.transform, m_InitScale2, flipX);
        else if (target == m_Child3)
            ApplyFlip(m_Child3.transform, m_InitScale3, flipX);
        else if (target == m_Child4)
            ApplyFlip(m_Child4.transform, m_InitScale4, flipX);
    }

    private static void ApplyFlip(Transform tr, Vector3 initScale, bool flipX)
    {
        if (tr == null)
            return;
        
        float x = Mathf.Abs(initScale.x) * (flipX ? -1f : 1f);
        tr.localScale = new Vector3(x, initScale.y, initScale.z);
    }
}
