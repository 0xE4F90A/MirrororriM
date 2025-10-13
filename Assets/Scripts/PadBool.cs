using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 真偽だけ返すゲームパッド入力ユーティリティ（新Input System）
/// ・どこからでも static に呼べる
/// ・*_Down / *_Held / *_Up を用意
/// ・D-Pad、トリガー、スティック（押し込み＆移動しきい値）にも対応
/// ・Gamepad未接続時は false
/// </summary>
public static class PadBool
{
    // スティック/トリガーの既定しきい値
    public const float DefaultStickThreshold = 0.20f;
    public const float DefaultTriggerThreshold = 0.10f;

    /// <summary>使うゲームパッド（nullなら current）</summary>
    private static Gamepad GP(Gamepad gp) => gp ?? Gamepad.current;

    /// <summary>パッド接続済み？</summary>
    public static bool HasPad(Gamepad gp = null) => GP(gp) != null;

    // ========= Face Buttons (A/B/X/Y or Cross/Circle/Square/Triangle) =========
    public static bool IsADown(Gamepad gp = null) => GP(gp)?.buttonSouth.wasPressedThisFrame ?? false;
    public static bool IsAHeld(Gamepad gp = null) => GP(gp)?.buttonSouth.isPressed ?? false;
    public static bool IsAUp(Gamepad gp = null) => GP(gp)?.buttonSouth.wasReleasedThisFrame ?? false;

    public static bool IsBDown(Gamepad gp = null) => GP(gp)?.buttonEast.wasPressedThisFrame ?? false;
    public static bool IsBHeld(Gamepad gp = null) => GP(gp)?.buttonEast.isPressed ?? false;
    public static bool IsBUp(Gamepad gp = null) => GP(gp)?.buttonEast.wasReleasedThisFrame ?? false;

    public static bool IsXDown(Gamepad gp = null) => GP(gp)?.buttonWest.wasPressedThisFrame ?? false;
    public static bool IsXHeld(Gamepad gp = null) => GP(gp)?.buttonWest.isPressed ?? false;
    public static bool IsXUp(Gamepad gp = null) => GP(gp)?.buttonWest.wasReleasedThisFrame ?? false;

    public static bool IsYDown(Gamepad gp = null) => GP(gp)?.buttonNorth.wasPressedThisFrame ?? false;
    public static bool IsYHeld(Gamepad gp = null) => GP(gp)?.buttonNorth.isPressed ?? false;
    public static bool IsYUp(Gamepad gp = null) => GP(gp)?.buttonNorth.wasReleasedThisFrame ?? false;

    // ========= Shoulders / Triggers (LB/RB/LT/RT or L1/R1/L2/R2) =========
    public static bool IsLBDown(Gamepad gp = null) => GP(gp)?.leftShoulder.wasPressedThisFrame ?? false;
    public static bool IsLBHeld(Gamepad gp = null) => GP(gp)?.leftShoulder.isPressed ?? false;
    public static bool IsLBUp(Gamepad gp = null) => GP(gp)?.leftShoulder.wasReleasedThisFrame ?? false;

    public static bool IsRBDown(Gamepad gp = null) => GP(gp)?.rightShoulder.wasPressedThisFrame ?? false;
    public static bool IsRBHeld(Gamepad gp = null) => GP(gp)?.rightShoulder.isPressed ?? false;
    public static bool IsRBUp(Gamepad gp = null) => GP(gp)?.rightShoulder.wasReleasedThisFrame ?? false;

    // トリガーは ButtonControl（press point 跨ぎで Down/Up 取れる）
    public static bool IsLTDown(Gamepad gp = null) => GP(gp)?.leftTrigger.wasPressedThisFrame ?? false;
    public static bool IsLTHeld(Gamepad gp = null) => GP(gp)?.leftTrigger.isPressed ?? false;
    public static bool IsLTUp(Gamepad gp = null) => GP(gp)?.leftTrigger.wasReleasedThisFrame ?? false;

    public static bool IsRTDown(Gamepad gp = null) => GP(gp)?.rightTrigger.wasPressedThisFrame ?? false;
    public static bool IsRTHeld(Gamepad gp = null) => GP(gp)?.rightTrigger.isPressed ?? false;
    public static bool IsRTUp(Gamepad gp = null) => GP(gp)?.rightTrigger.wasReleasedThisFrame ?? false;

    // しきい値版（アナログ値で判定したいとき）
    public static bool IsLTOver(float threshold = DefaultTriggerThreshold, Gamepad gp = null)
        => (GP(gp)?.leftTrigger.ReadValue() ?? 0f) >= threshold;
    public static bool IsRTOver(float threshold = DefaultTriggerThreshold, Gamepad gp = null)
        => (GP(gp)?.rightTrigger.ReadValue() ?? 0f) >= threshold;

    // ========= Stick Click (L3/R3) =========
    public static bool IsL3Down(Gamepad gp = null) => GP(gp)?.leftStickButton.wasPressedThisFrame ?? false;
    public static bool IsL3Held(Gamepad gp = null) => GP(gp)?.leftStickButton.isPressed ?? false;
    public static bool IsL3Up(Gamepad gp = null) => GP(gp)?.leftStickButton.wasReleasedThisFrame ?? false;

    public static bool IsR3Down(Gamepad gp = null) => GP(gp)?.rightStickButton.wasPressedThisFrame ?? false;
    public static bool IsR3Held(Gamepad gp = null) => GP(gp)?.rightStickButton.isPressed ?? false;
    public static bool IsR3Up(Gamepad gp = null) => GP(gp)?.rightStickButton.wasReleasedThisFrame ?? false;

    // ========= Start / Select (Options / Share) =========
    public static bool IsStartDown(Gamepad gp = null) => GP(gp)?.startButton.wasPressedThisFrame ?? false;
    public static bool IsStartHeld(Gamepad gp = null) => GP(gp)?.startButton.isPressed ?? false;
    public static bool IsStartUp(Gamepad gp = null) => GP(gp)?.startButton.wasReleasedThisFrame ?? false;

    public static bool IsSelectDown(Gamepad gp = null) => GP(gp)?.selectButton.wasPressedThisFrame ?? false;
    public static bool IsSelectHeld(Gamepad gp = null) => GP(gp)?.selectButton.isPressed ?? false;
    public static bool IsSelectUp(Gamepad gp = null) => GP(gp)?.selectButton.wasReleasedThisFrame ?? false;

    // ========= D-Pad =========
    public static bool IsDpadUpDown(Gamepad gp = null) => GP(gp)?.dpad.up.wasPressedThisFrame ?? false;
    public static bool IsDpadUpHeld(Gamepad gp = null) => GP(gp)?.dpad.up.isPressed ?? false;
    public static bool IsDpadUpUp(Gamepad gp = null) => GP(gp)?.dpad.up.wasReleasedThisFrame ?? false;

    public static bool IsDpadDownDown(Gamepad gp = null) => GP(gp)?.dpad.down.wasPressedThisFrame ?? false;
    public static bool IsDpadDownHeld(Gamepad gp = null) => GP(gp)?.dpad.down.isPressed ?? false;
    public static bool IsDpadDownUp(Gamepad gp = null) => GP(gp)?.dpad.down.wasReleasedThisFrame ?? false;

    public static bool IsDpadLeftDown(Gamepad gp = null) => GP(gp)?.dpad.left.wasPressedThisFrame ?? false;
    public static bool IsDpadLeftHeld(Gamepad gp = null) => GP(gp)?.dpad.left.isPressed ?? false;
    public static bool IsDpadLeftUp(Gamepad gp = null) => GP(gp)?.dpad.left.wasReleasedThisFrame ?? false;

    public static bool IsDpadRightDown(Gamepad gp = null) => GP(gp)?.dpad.right.wasPressedThisFrame ?? false;
    public static bool IsDpadRightHeld(Gamepad gp = null) => GP(gp)?.dpad.right.isPressed ?? false;
    public static bool IsDpadRightUp(Gamepad gp = null) => GP(gp)?.dpad.right.wasReleasedThisFrame ?? false;

    // ========= Sticks moved (アナログの動き判定) =========
    public static bool IsLeftStickMoved(float threshold = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().sqrMagnitude ?? 0f) >= threshold * threshold;

    public static bool IsRightStickMoved(float threshold = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().sqrMagnitude ?? 0f) >= threshold * threshold;

    // 方向別（簡易）
    public static bool IsLeftStickRight(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().x ?? 0f) >= th;
    public static bool IsLeftStickLeft(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().x ?? 0f) <= -th;
    public static bool IsLeftStickUp(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().y ?? 0f) >= th;
    public static bool IsLeftStickDown(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().y ?? 0f) <= -th;

    public static bool IsRightStickRight(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().x ?? 0f) >= th;
    public static bool IsRightStickLeft(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().x ?? 0f) <= -th;
    public static bool IsRightStickUp(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().y ?? 0f) >= th;
    public static bool IsRightStickDown(float th = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().y ?? 0f) <= -th;

    // ========= 便利：任意の「押された/押されてる/離された」 =========
    public static bool AnyDown(Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        return
            g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame ||
            g.buttonWest.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame ||
            g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame ||
            g.leftStickButton.wasPressedThisFrame || g.rightStickButton.wasPressedThisFrame ||
            g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame ||
            g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame ||
            g.dpad.left.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame ||
            g.leftTrigger.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame;
    }

    public static bool AnyHeld(Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        return
            g.buttonSouth.isPressed || g.buttonEast.isPressed ||
            g.buttonWest.isPressed || g.buttonNorth.isPressed ||
            g.leftShoulder.isPressed || g.rightShoulder.isPressed ||
            g.leftStickButton.isPressed || g.rightStickButton.isPressed ||
            g.startButton.isPressed || g.selectButton.isPressed ||
            g.dpad.IsPressed() ||
            g.leftTrigger.isPressed || g.rightTrigger.isPressed;
    }

    public static bool AnyUp(Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        return
            g.buttonSouth.wasReleasedThisFrame || g.buttonEast.wasReleasedThisFrame ||
            g.buttonWest.wasReleasedThisFrame || g.buttonNorth.wasReleasedThisFrame ||
            g.leftShoulder.wasReleasedThisFrame || g.rightShoulder.wasReleasedThisFrame ||
            g.leftStickButton.wasReleasedThisFrame || g.rightStickButton.wasReleasedThisFrame ||
            g.startButton.wasReleasedThisFrame || g.selectButton.wasReleasedThisFrame ||
            g.dpad.up.wasReleasedThisFrame || g.dpad.down.wasReleasedThisFrame ||
            g.dpad.left.wasReleasedThisFrame || g.dpad.right.wasReleasedThisFrame ||
            g.leftTrigger.wasReleasedThisFrame || g.rightTrigger.wasReleasedThisFrame;
    }
}
