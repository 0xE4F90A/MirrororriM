using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class CatSelect : MonoBehaviour
{
    [Header("====== 表示に使う 8 つの子オブジェクト（インスペクターで割当） ======")]
    [SerializeField] private GameObject m_FrontWait_L;
    [SerializeField] private GameObject m_FrontWait_R;
    [SerializeField] private GameObject m_BackWait_L;
    [SerializeField] private GameObject m_BackWait_R;
    [SerializeField] private GameObject m_FrontWalk_L;
    [SerializeField] private GameObject m_FrontWalk_R;
    [SerializeField] private GameObject m_BackWalk_L;
    [SerializeField] private GameObject m_BackWalk_R;

    [Header("====== 移動設定 ======")]
    [SerializeField] private float m_MoveSpeed = 2.0f;
    [SerializeField, Tooltip("true: XZ 平面で移動（3D用途） / false: XY 平面で移動（2D用途）")]
    private bool m_MoveInXZPlane = true;

    [SerializeField, Tooltip("XZ移動時にYを固定")]
    private bool m_FreezeYPosition = true;

    [SerializeField, Tooltip("移動方向を反転する（必要に応じてON）")]
    private bool m_InvertMoveDirection = false;

    private enum Dir { None = 0, Right = 1, Left = 2, Up = 3, Down = 4 }

    // 「最後に押されたが、まだ押下中のキー」を選ぶための状態
    private readonly int[] m_LastDownFrame = new int[5]; // Dir を添字化（None含むが未使用）
    private readonly bool[] m_IsHeld = new bool[5];

    // 現在の有効方向（表示と移動の両方で使用）
    private Dir m_ActiveDir = Dir.None;

    // 物理移動
    private Rigidbody m_Rigidbody;
    private Vector3 m_MoveDir3D = Vector3.zero;

    // 現在表示中（Animatorリセット防止）
    private GameObject m_CurrentActive;

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.isKinematic = false;
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        if (m_MoveInXZPlane && m_FreezeYPosition)
        {
            m_Rigidbody.constraints |= RigidbodyConstraints.FreezePositionY;
        }

        // 初期化
        for (int i = 0; i < m_LastDownFrame.Length; ++i)
        {
            m_LastDownFrame[i] = int.MinValue; // まだ一度も押されていない
            m_IsHeld[i] = false;
        }

        // 初期：全OFF→FrontWait_Rを表示（お好みで）
        ForceAllInactive();
        SwitchActive(m_FrontWait_R);
    }

    private void Update()
    {
        // 押下状態を更新（GetKey / GetKeyDown / GetKeyUp を全部見る）
        UpdateKeyState(Dir.Right, GetKeyRight(), GetKeyDownRight(), GetKeyUpRight());
        UpdateKeyState(Dir.Left, GetKeyLeft(), GetKeyDownLeft(), GetKeyUpLeft());
        UpdateKeyState(Dir.Up, GetKeyUp(), GetKeyDownUp(), GetKeyUpUp());
        UpdateKeyState(Dir.Down, GetKeyDown(), GetKeyDownDown(), GetKeyUpDown());

        // 今押されているキーの中から「最後に押されたもの」を選ぶ
        Dir selected = SelectLatestHeldDir();

        // 表示（Walk/Wait）を切り替え
        if (selected != Dir.None)
        {
            if (selected != m_ActiveDir)
            {
                ShowWalkFor(selected);
                m_ActiveDir = selected;
            }
            else
            {
                // 同じ方向を継続押下中なら毎フレーム同じWalkを維持
                ShowWalkFor(m_ActiveDir);
            }
        }
        else
        {
            // 何も押されていない：直前の方向に対応するWaitを表示
            if (m_ActiveDir != Dir.None)
            {
                ShowWaitFor(m_ActiveDir);
                m_ActiveDir = Dir.None;
            }
        }

        // 移動ベクトル（表示と同じ基準＝m_ActiveDir で決定）
        m_MoveDir3D = DirToMoveVector(m_ActiveDir);
    }

    private void FixedUpdate()
    {
        if (m_Rigidbody == null) return;

        if (m_MoveDir3D.sqrMagnitude > 0f)
        {
            Vector3 dir = m_MoveDir3D.normalized;
            if (m_InvertMoveDirection) dir = -dir; // 反転はここで

            Vector3 delta = dir * (m_MoveSpeed * Time.fixedDeltaTime);
            m_Rigidbody.MovePosition(m_Rigidbody.position + delta);
        }
    }

    //==== 入力ヘルパ ====
    private static bool GetKeyRight() => Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || PadBool.IsRight();
    private static bool GetKeyLeft() => Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || PadBool.IsLeft();
    private static bool GetKeyUp() => Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || PadBool.IsUp();
    private static bool GetKeyDown() => Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || PadBool.IsDown();

    private static bool GetKeyDownRight() => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || PadBool.IsRight();
    private static bool GetKeyDownLeft() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || PadBool.IsLeft();
    private static bool GetKeyDownUp() => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || PadBool.IsUp();
    private static bool GetKeyDownDown() => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || PadBool.IsDown();

    private static bool GetKeyUpRight() => Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D);
    private static bool GetKeyUpLeft() => Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A);
    private static bool GetKeyUpUp() => Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W);
    private static bool GetKeyUpDown() => Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S);

    private static int Idx(Dir d) => (int)d;

    private void UpdateKeyState(Dir d, bool held, bool down, bool up)
    {
        int i = Idx(d);

        if (down)
        {
            m_LastDownFrame[i] = Time.frameCount; // 押されたフレームを記録
        }

        // 現在押下中かどうかは毎フレーム上書き
        if (up) m_IsHeld[i] = false;
        else m_IsHeld[i] = held;
    }

    private Dir SelectLatestHeldDir()
    {
        // 押下中の中で LastDownFrame が最大（＝最後に押された）のものを選ぶ
        Dir best = Dir.None;
        int bestFrame = int.MinValue;

        // 右→左→上→下の順で同時押し同フレームのタイブレーク（最後に評価した方を優先したければ >= にする）
        Check(Dir.Right);
        Check(Dir.Left);
        Check(Dir.Up);
        Check(Dir.Down);
        return best;

        void Check(Dir d)
        {
            int i = Idx(d);
            if (!m_IsHeld[i]) return;
            if (m_LastDownFrame[i] > bestFrame) // 同フレーム同時押しは評価順で決まる
            {
                best = d;
                bestFrame = m_LastDownFrame[i];
            }
        }
    }

    private Vector3 DirToMoveVector(Dir d)
    {
        // 上/下は見た目だけに使用し、移動は左右のみ
        switch (d)
        {
            case Dir.Right:
                return m_MoveInXZPlane ? new Vector3(+1f, 0f, 0f) : new Vector3(+1f, 0f, 0f);
            case Dir.Left:
                return m_MoveInXZPlane ? new Vector3(-1f, 0f, 0f) : new Vector3(-1f, 0f, 0f);
            default:
                return Vector3.zero;
        }
    }

    //==== 表示：指定マッピング（ご要望に合わせてこのまま） ====

    private void ShowWalkFor(Dir d)
    {
        switch (d)
        {
            case Dir.Right: SwitchActive(m_BackWalk_R); break;
            case Dir.Left: SwitchActive(m_FrontWalk_L); break;
            case Dir.Up: SwitchActive(m_BackWalk_L); break;
            case Dir.Down: SwitchActive(m_FrontWalk_R); break;
        }
    }

    private void ShowWaitFor(Dir d)
    {
        switch (d)
        {
            case Dir.Right: SwitchActive(m_BackWait_R); break;
            case Dir.Left: SwitchActive(m_FrontWait_L); break;
            case Dir.Up: SwitchActive(m_BackWait_L); break;
            case Dir.Down: SwitchActive(m_FrontWait_R); break;
        }
    }

    //==== アクティブ切替（Animatorリセット防止） ====

    private void SwitchActive(GameObject target)
    {
        if (target == null) return;
        if (m_CurrentActive == target) return; // 同一なら何もしない

        if (m_CurrentActive != null) m_CurrentActive.SetActive(false);
        target.SetActive(true);
        m_CurrentActive = target;
    }

    private void ForceAllInactive()
    {
        SafeSetActive(m_FrontWait_L, false);
        SafeSetActive(m_FrontWait_R, false);
        SafeSetActive(m_BackWait_L, false);
        SafeSetActive(m_BackWait_R, false);
        SafeSetActive(m_FrontWalk_L, false);
        SafeSetActive(m_FrontWalk_R, false);
        SafeSetActive(m_BackWalk_L, false);
        SafeSetActive(m_BackWalk_R, false);
        m_CurrentActive = null;
    }

    private static void SafeSetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
