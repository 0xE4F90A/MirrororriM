using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitleModelRotate : MonoBehaviour
{
    [Header("傾ける角度（度）")]
    [SerializeField] private float m_TiltDegrees = 2.0f;

    [Header("スムージング（0で即時／大きいほど素早く追従）")]
    [Min(0f)]
    [SerializeField] private float m_SmoothSpeed = 10.0f;

    [Header("OnEnable時に現在の姿勢を“基準姿勢”として再キャプチャする")]
    [SerializeField] private bool m_ReCaptureBaseOnEnable = true;

    // 起点となるローカル回転（キーを離したときに戻る先）
    private Quaternion m_BaseLocalRotation;

    private void Awake()
    {
        m_BaseLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (m_ReCaptureBaseOnEnable)
        {
            m_BaseLocalRotation = transform.localRotation;
        }
    }

    private void Update()
    {
        // 矢印キー入力に応じてオフセットEuler角を構成
        // 仕様：
        // 右→X +2°, 左→Z +2°, 上→Y +2°, 下→Y -2°
        Vector3 eulerOffset = Vector3.zero;

        if (Input.GetKey(KeyCode.RightArrow) || PadBool.IsRightStickRight() || PadBool.IsLeftStickRight() || PadBool.IsDpadRightHeld())
        {
            eulerOffset.y += m_TiltDegrees;
        }
        if (Input.GetKey(KeyCode.LeftArrow) || PadBool.IsRightStickLeft() || PadBool.IsLeftStickLeft() || PadBool.IsDpadLeftHeld())
        {
            eulerOffset.y -= m_TiltDegrees;
        }
        if (Input.GetKey(KeyCode.UpArrow) || PadBool.IsRightStickUp() || PadBool.IsLeftStickUp() || PadBool.IsDpadUpHeld())
        {
            eulerOffset.x += m_TiltDegrees;
        }
        if (Input.GetKey(KeyCode.DownArrow) || PadBool.IsRightStickDown() || PadBool.IsLeftStickDown() || PadBool.IsDpadDownHeld())
        {
            eulerOffset.x -= m_TiltDegrees;
        }

        // 目標回転 = 基準 * オフセット
        Quaternion target = m_BaseLocalRotation * Quaternion.Euler(eulerOffset);

        if (m_SmoothSpeed <= 0f)
        {
            // スムージングなし（即時反映）
            transform.localRotation = target;
        }
        else
        {
            // 指数スムージング（フレームレート非依存）
            float t = 1f - Mathf.Exp(-m_SmoothSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, t);
        }
    }

    /// <summary>
    /// 現在のローカル回転を“基準姿勢”として手動再キャプチャ。
    /// </summary>
    public void CaptureCurrentAsBase()
    {
        m_BaseLocalRotation = transform.localRotation;
    }

    /// <summary>
    /// 基準姿勢へ即時リセット（見た目も即時戻す）。
    /// </summary>
    public void ResetToBaseInstant()
    {
        transform.localRotation = m_BaseLocalRotation;
    }
}
