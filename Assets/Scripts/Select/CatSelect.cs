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

    private enum Dir { None, Right, Left, Up, Down }

    // ラッチ中のキー（離すまで他キー無視）
    private Dir m_Latched = Dir.None;

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
        if (m_MoveInXZPlane && m_FreezeYPosition) m_Rigidbody.constraints |= RigidbodyConstraints.FreezePositionY;

        // 初期：全OFF→FrontWait_Rを表示
        ForceAllInactive();
        SwitchActive(m_FrontWait_R);
    }

    private void Update()
    {
        // 1) ラッチされていないとき：最初に押したキーをラッチ
        if (m_Latched == Dir.None)
        {
            // 同フレーム複数押下はこの優先順でラッチ（必要なら順序を変えてOK）
            if (GetKeyDownRight()) m_Latched = Dir.Right;
            else if (GetKeyDownLeft()) m_Latched = Dir.Left;
            else if (GetKeyDownUp()) m_Latched = Dir.Up;
            else if (GetKeyDownDown()) m_Latched = Dir.Down;

            if (m_Latched != Dir.None)
            {
                ShowWalkFor(m_Latched);
            }
        }
        // 2) ラッチ中：そのキーだけを受け付ける
        else
        {
            // 押し続けている間はWalkを表示（同一GOなら何もしない）
            if (IsHeld(m_Latched))
            {
                ShowWalkFor(m_Latched);
            }
            else
            {
                // 離された瞬間：対応するWaitを表示し、ラッチ解除
                ShowWaitFor(m_Latched);
                m_Latched = Dir.None;
            }
        }

        // 入力→移動ベクトル（デフォは左右のみ移動。上/下は見た目専用）
        m_MoveDir3D = ReadMoveDirection3D();
    }

    private void FixedUpdate()
    {
        if (m_Rigidbody == null) return;

        if (m_MoveDir3D.sqrMagnitude > 0f)
        {
            float sign = m_InvertMoveDirection ? 1f : -1f;
            Vector3 delta = m_MoveDir3D.normalized * (m_MoveSpeed * sign) * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + delta);
        }
    }

    //==== 入力ヘルパ ====
    private static bool GetKeyRight() => Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
    private static bool GetKeyLeft() => Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
    private static bool GetKeyUp() => Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
    private static bool GetKeyDown() => Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);

    private static bool GetKeyDownRight() => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    private static bool GetKeyDownLeft() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    private static bool GetKeyDownUp() => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    private static bool GetKeyDownDown() => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);

    private static bool IsHeld(Dir d)
    {
        switch (d)
        {
            case Dir.Right: return GetKeyRight();
            case Dir.Left: return GetKeyLeft();
            case Dir.Up: return GetKeyUp();
            case Dir.Down: return GetKeyDown();
            default: return false;
        }
    }

    private Vector3 ReadMoveDirection3D()
    {
        // 右/左のみ移動（上/下は見た目だけ）
        int x = 0;
        if (GetKeyRight()) x += 1;
        if (GetKeyLeft()) x -= 1;

        if (x == 0) return Vector3.zero;

        if (m_MoveInXZPlane) return new Vector3(x, 0f, 0f).normalized;
        else return new Vector3(x, 0f, 0f).normalized;
    }

    //==== 表示：指定マッピング ====

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
