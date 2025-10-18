using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectClampMover : MonoBehaviour
{
    //========================
    // 依存（ロック判定側）
    //========================
    [Header("=== 連携（任意） ===")]
    [SerializeField, Tooltip("ロック状態とSE(ブロック)用。未設定でも動作します")]
    private ObjectLocker m_Locker;

    //========================
    // 定義
    //========================
    public enum Axis { X, Y, Z }

    [Serializable]
    public struct AxisClamp
    {
        [Tooltip("この軸のクランプを有効化")] public bool Enabled;
        [Tooltip("最小値（ワールド座標）")] public float Min;
        [Tooltip("最大値（ワールド座標）")] public float Max;
    }

    [Serializable]
    public struct MoveKey
    {
        [Tooltip("移動に使うキー（GetKeyDown：長押し無効）")] public KeyCode Key;
        [Tooltip("ワールド方向（例：右へなら (1,0,0)）")] public Vector3 WorldDirection;
        [Tooltip("ワープ距離（例：1.25）")] public float Step;
    }

    [Serializable]
    public sealed class FixedRotation
    {
        [Header("入力")]
        public KeyCode Key = KeyCode.None;

        [Header("回転設定（ローカル）")]
        public Axis Axis = Axis.Y;
        [Tooltip("角度パターン（度）。例：0,45,90 → ピンポン 0→45→90→45→0…")]
        public float[] Angles = new float[] { 0f, 90f };

        [Header("サウンド")]
        public AudioClip Se;

        [Header("初回押下の挙動")]
        public bool StartFromFirst = true;
        public bool SkipFirstIfSame = true;
        public float FirstPressEps = 0.1f;

        [NonSerialized] public int _index = 0;
        [NonSerialized] public int _direction = +1;
        [NonSerialized] public bool _appliedOnce = false;

        public void ResetRuntime() { _index = 0; _direction = +1; _appliedOnce = false; }
    }

    private enum RotationOrder { XYZ, XZY, YXZ, YZX, ZXY, ZYX }

    //========================
    // インスペクター：移動・クランプ
    //========================
    [Header("=== ワープ（ワールド座標・GetKeyDownのみ） ===")]
    [SerializeField] private MoveKey m_MoveRight = new MoveKey { Key = KeyCode.RightArrow, WorldDirection = new Vector3(1f, 0f, 0f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveLeft = new MoveKey { Key = KeyCode.LeftArrow, WorldDirection = new Vector3(-1f, 0f, 0f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveUp = new MoveKey { Key = KeyCode.UpArrow, WorldDirection = new Vector3(0f, 0f, 1f), Step = 1.25f };
    [SerializeField] private MoveKey m_MoveDown = new MoveKey { Key = KeyCode.DownArrow, WorldDirection = new Vector3(0f, 0f, -1f), Step = 1.25f };

    [Header("移動SE（SE1）")]
    [SerializeField] private AudioClip m_SeMove;
    [SerializeField, Tooltip("SE再生用。未割当でも動作します")] private AudioSource m_AudioSource;

    [Header("=== ワールド座標クランプ ===")]
    [SerializeField] private AxisClamp m_ClampX = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampY = new AxisClamp { Enabled = false, Min = 0f, Max = 100f };
    [SerializeField] private AxisClamp m_ClampZ = new AxisClamp { Enabled = false, Min = -100f, Max = 100f };

    //========================
    // 回転合成
    //========================
    [Header("=== 回転合成設定 ===")]
    [SerializeField, Tooltip("合成順序（既定：Yaw→Pitch→Roll の YXZ）")]
    private RotationOrder m_Order = RotationOrder.YXZ;

    private Quaternion m_BaseLocalRotation; // 合成用の基準（自身の起動時ローカル回転）
    private float m_CmdAngleX, m_CmdAngleY, m_CmdAngleZ;

    //========================
    // ライフサイクル
    //========================
    private void Awake()
    {
        if (!m_AudioSource) m_AudioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        m_BaseLocalRotation = transform.localRotation;
        m_CmdAngleX = m_CmdAngleY = m_CmdAngleZ = 0f;

        if (m_Locker) m_Locker.GetLocked(); // 初期状態チェック
    }

    private void Update()
    {
        // ロック中は操作ブロック
        if (m_Locker && m_Locker.GetLocked())
        {
            if (AnyControlKeyDown()) m_Locker.GetLocked();
            return;
        }

        HandleMoveKey(in m_MoveRight);
        HandleMoveKey(in m_MoveLeft);
        HandleMoveKey(in m_MoveUp);
        HandleMoveKey(in m_MoveDown);

        HandleFixedRotation(m_RotationA);
        HandleFixedRotation(m_RotationB);
    }

    private void OnValidate()
    {
        if (m_ClampX.Enabled && m_ClampX.Min > m_ClampX.Max) Swap(ref m_ClampX.Min, ref m_ClampX.Max);
        if (m_ClampY.Enabled && m_ClampY.Min > m_ClampY.Max) Swap(ref m_ClampY.Min, ref m_ClampY.Max);
        if (m_ClampZ.Enabled && m_ClampZ.Min > m_ClampZ.Max) Swap(ref m_ClampZ.Min, ref m_ClampZ.Max);
    }

    private static void Swap(ref float a, ref float b) { float t = a; a = b; b = t; }

    //========================
    // 入力ユーティリティ
    //========================
    private bool AnyControlKeyDown()
    {
        if (Input.GetKeyDown(m_MoveRight.Key)) return true;
        if (Input.GetKeyDown(m_MoveLeft.Key)) return true;
        if (Input.GetKeyDown(m_MoveUp.Key)) return true;
        if (Input.GetKeyDown(m_MoveDown.Key)) return true;
        if (m_RotationA != null && m_RotationA.Key != KeyCode.None && (Input.GetKeyDown(m_RotationA.Key) || PadBool.IsYDown())) return true;
        if (m_RotationB != null && m_RotationB.Key != KeyCode.None && (Input.GetKeyDown(m_RotationB.Key) || PadBool.IsXDown())) return true;
        return false;
    }

    //========================
    // PadBool 連携ヘルパ
    //========================
    private static bool IsPadMoveDown(in MoveKey mk, bool padMode)
    {
        // WorldDirection の主要成分で判定（X優先、次にZ）
        Vector3 d = mk.WorldDirection;
        if (d.sqrMagnitude <= 0f) return false;

        // どの軸が支配的かで分岐（X or Z）。Yは不使用。
        float ax = Mathf.Abs(d.x);
        float az = Mathf.Abs(d.z);


        if (ax >= az)
        {
            if (d.x > 0f) return padMode ? PadBool.IsUpDown(PadBool.DirInputSource.RStick) : PadBool.IsRightDown(PadBool.DirInputSource.RStick);
            if (d.x < 0f) return padMode ? PadBool.IsDownDown(PadBool.DirInputSource.RStick) : PadBool.IsLeftDown(PadBool.DirInputSource.RStick);
        }
        else
        {
            if (d.z > 0f) return padMode ? PadBool.IsLeftDown(PadBool.DirInputSource.RStick) : PadBool.IsUpDown(PadBool.DirInputSource.RStick);
            if (d.z < 0f) return padMode ? PadBool.IsRightDown(PadBool.DirInputSource.RStick) : PadBool.IsDownDown(PadBool.DirInputSource.RStick);
        }
        return false;

    }

    private bool IsPadRotationDown(FixedRotation rot)
    {
        // 既存の AnyControlKeyDown と同じ割当：
        //  - m_RotationA → Y ボタン
        //  - m_RotationB → X ボタン
        if (rot == null) return false;
        if (ReferenceEquals(rot, m_RotationA)) return PadBool.IsYDown();
        if (ReferenceEquals(rot, m_RotationB)) return PadBool.IsXDown();
        return false;
    }


    //========================
    // ワープ処理（ワールド）
    //========================
    private void HandleMoveKey(in MoveKey mk)
    {
        if (mk.Key == KeyCode.None) return;

        // ▼ここを変更：キーボード Down か、PadBool の方向 Down のどちらかで発火
        if (!Input.GetKeyDown(mk.Key) && !IsPadMoveDown(in mk, true)) return;

        Vector3 dir = mk.WorldDirection;
        if (dir.sqrMagnitude <= 0f) return;

        dir.Normalize();
        Vector3 oldPos = transform.position;
        Vector3 newPos = ApplyClampWorld(oldPos + dir * mk.Step);

        if ((newPos - oldPos).sqrMagnitude > 1e-8f)
        {
            transform.position = newPos;
            PlayOneShotSafe(m_SeMove);
            if (m_Locker) m_Locker.GetLocked();
        }
    }


    private Vector3 ApplyClampWorld(Vector3 pos)
    {
        if (m_ClampX.Enabled) pos.x = Mathf.Clamp(pos.x, m_ClampX.Min, m_ClampX.Max);
        if (m_ClampY.Enabled) pos.y = Mathf.Clamp(pos.y, m_ClampY.Min, m_ClampY.Max);
        if (m_ClampZ.Enabled) pos.z = Mathf.Clamp(pos.z, m_ClampZ.Min, m_ClampZ.Max);
        return pos;
    }

    //========================
    // 回転処理（固定2系統・ローカル・ピンポン）
    //========================
    [Header("=== 回転（固定2系統：A / B、ローカル） ===")]
    [SerializeField] private FixedRotation m_RotationA = new FixedRotation { Key = KeyCode.Y, Axis = Axis.Y, Angles = new float[] { 0f, 90f } };
    [SerializeField] private FixedRotation m_RotationB = new FixedRotation { Key = KeyCode.U, Axis = Axis.Z, Angles = new float[] { 0f, 90f } };

    private void HandleFixedRotation(FixedRotation rot)
    {
        if (rot == null || rot.Key == KeyCode.None) return;

        // ▼ここを変更：キーボード Down か、PadBool の（A=Y / B=X）Down のどちらかで発火
        if (!Input.GetKeyDown(rot.Key) && !IsPadRotationDown(rot)) return;

        int count = (rot.Angles != null) ? rot.Angles.Length : 0;
        if (count <= 0) return;

        int last = count - 1;
        int idxToApply;

        if (!rot._appliedOnce && rot.StartFromFirst)
        {
            idxToApply = 0;
            if (rot.SkipFirstIfSame && last >= 1)
            {
                float target0 = rot.Angles[0];
                float current = GetCommandedAngle(rot.Axis);
                if (Mathf.Abs(Mathf.DeltaAngle(target0, current)) <= rot.FirstPressEps)
                    idxToApply = 1;
            }
        }
        else
        {
            idxToApply = Mathf.Clamp(rot._index, 0, last);
        }

        float angle = NormalizeAngle(rot.Angles[idxToApply]);
        SetCommandedAngle(rot.Axis, angle);
        RebuildLocalRotation();
        PlayOneShotSafe(rot.Se);

        if (last >= 1)
        {
            int dir = rot._direction;
            if (idxToApply <= 0) dir = +1;
            else if (idxToApply >= last) dir = -1;
            rot._direction = dir;
            rot._index = Mathf.Clamp(idxToApply + dir, 0, last);
        }
        else
        {
            rot._index = 0; rot._direction = +1;
        }

        rot._appliedOnce = true;

        if (m_Locker) m_Locker.GetLocked();
    }


    private float GetCommandedAngle(Axis axis) => axis switch
    {
        Axis.X => m_CmdAngleX,
        Axis.Y => m_CmdAngleY,
        _ => m_CmdAngleZ,
    };

    private void SetCommandedAngle(Axis axis, float angleDeg)
    {
        switch (axis)
        {
            case Axis.X: m_CmdAngleX = angleDeg; break;
            case Axis.Y: m_CmdAngleY = angleDeg; break;
            case Axis.Z: m_CmdAngleZ = angleDeg; break;
        }
    }

    private void RebuildLocalRotation()
    {
        Quaternion qx = Quaternion.AngleAxis(m_CmdAngleX, Vector3.right);
        Quaternion qy = Quaternion.AngleAxis(m_CmdAngleY, Vector3.up);
        Quaternion qz = Quaternion.AngleAxis(m_CmdAngleZ, Vector3.forward);

        Quaternion r = m_Order switch
        {
            RotationOrder.XYZ => qx * qy * qz,
            RotationOrder.XZY => qx * qz * qy,
            RotationOrder.YXZ => qy * qx * qz, // 既定
            RotationOrder.YZX => qy * qz * qx,
            RotationOrder.ZXY => qz * qx * qy,
            _ => qz * qy * qx,
        };

        transform.localRotation = m_BaseLocalRotation * r;
    }

    private static float NormalizeAngle(float a) => Mathf.Repeat(a, 360f);

    //========================
    // オーディオ
    //========================
    private void PlayOneShotSafe(AudioClip clip)
    {
        if (!clip) return;
        if (m_AudioSource) m_AudioSource.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
