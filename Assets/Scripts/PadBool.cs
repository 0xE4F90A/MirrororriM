using System.Collections.Generic;
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

    // ====== Schmitt / Opposite-Guard 設定（必要に応じて変更可） ======
    public static float StickHysteresisHi = 0.45f; // 方向「入る」しきい値（大きめ）
    public static float StickHysteresisLo = 0.25f; // 方向「抜ける」しきい値（小さめ）
    public static int StickOppositeGuardFrames = 2; // 中立をこのフレーム数維持後のみ反対方向へ

    // 内部：各デバイスごとのスティック軸のラッチ状態（-1,0,+1）と中立フレーム
    private struct StickSchmittState
    {
        public int Frame;
        public sbyte Lx, Ly, Rx, Ry;      // -1,0,+1 ラッチ結果
        public int LxZeroF, LyZeroF, RxZeroF, RyZeroF; // 中立(0)だった最後のフレーム

        // 各スティックの「動いている」状態とそのエッジ
        public bool LMoved, LMovedDown, LMovedUp;
        public bool RMoved, RMovedDown, RMovedUp;
    }
    private static readonly Dictionary<int, StickSchmittState> s_Schmitt = new Dictionary<int, StickSchmittState>();

    private static sbyte UpdateAxisWithHysteresis(ref sbyte state, ref int lastZeroFrame, float value, int frame)
    {
        float hi = StickHysteresisHi;
        float lo = StickHysteresisLo;

        float a = Mathf.Abs(value);
        sbyte sign = (value >= 0f) ? (sbyte)+1 : (sbyte)-1;

        // 中立ゾーン
        if (a <= lo)
        {
            state = 0;
            lastZeroFrame = frame; // 中立を通過
            return state;
        }

        if (state == 0)
        {
            // 0 → ±1：入閾値を超えたら遷移
            if (a >= hi) state = sign;
            return state;
        }

        // いま ±1 方向にいる場合
        if (sign == state)
        {
            // 同じ向き：保持（抜けは上の lo 判定で既に0になる）
            return state;
        }
        else
        {
            // 反対向きへ行きたい：中立を StickOppositeGuardFrames 以上維持してから、
            // かつ入閾値を超えた場合のみ許可。それまでは現状態を維持。
            if (frame - lastZeroFrame >= StickOppositeGuardFrames && a >= hi)
            {
                state = sign;
            }
            return state;
        }
    }

    private static StickSchmittState EnsureSchmitt(Gamepad g)
    {
        int id = g.deviceId;
        s_Schmitt.TryGetValue(id, out var st);
        if (st.Frame == Time.frameCount) return st;

        Vector2 L = g.leftStick.ReadValue();
        Vector2 R = g.rightStick.ReadValue();
        int f = Time.frameCount;

        // 前フレームの「動いてる？」（どちらかの軸が非0）を保持
        bool prevLMoved = (st.Lx != 0 || st.Ly != 0);
        bool prevRMoved = (st.Rx != 0 || st.Ry != 0);

        // 各軸を更新（-1/0/+1 にラッチ）
        st.Lx = UpdateAxisWithHysteresis(ref st.Lx, ref st.LxZeroF, L.x, f);
        st.Ly = UpdateAxisWithHysteresis(ref st.Ly, ref st.LyZeroF, L.y, f);
        st.Rx = UpdateAxisWithHysteresis(ref st.Rx, ref st.RxZeroF, R.x, f);
        st.Ry = UpdateAxisWithHysteresis(ref st.Ry, ref st.RyZeroF, R.y, f);

        // 今フレームの「動いてる？」を算出し、Down/Up を生成
        bool nowLMoved = (st.Lx != 0 || st.Ly != 0);
        bool nowRMoved = (st.Rx != 0 || st.Ry != 0);

        st.LMovedDown = (nowLMoved && !prevLMoved);
        st.LMovedUp = (!nowLMoved && prevLMoved);
        st.LMoved = nowLMoved;

        st.RMovedDown = (nowRMoved && !prevRMoved);
        st.RMovedUp = (!nowRMoved && prevRMoved);
        st.RMoved = nowRMoved;

        st.Frame = f;
        s_Schmitt[id] = st;
        return st;
    }

    // StickSchmittState を返す安全ヘルパ（Gamepad未接続なら false）
    private static bool TryGetSchmitt(Gamepad gp, out StickSchmittState st)
    {
        var g = GP(gp);
        if (g == null) { st = default; return false; }
        st = EnsureSchmitt(g);
        return true;
    }


    // === Left stick: moved エッジ ===
    public static bool IsLeftStickMovedUp(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.LMovedUp;
    public static bool IsLeftStickMovedDown(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.LMovedDown;
    public static bool IsLeftStickMovedHeld(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.LMoved;

    // === Right stick: moved エッジ ===
    public static bool IsRightStickMovedUp(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.RMovedUp;
    public static bool IsRightStickMovedDown(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.RMovedDown;
    public static bool IsRightStickMovedHeld(Gamepad gp = null)
        => TryGetSchmitt(gp, out var ss) && ss.RMoved;

    public static bool IsLeftStickReleased(Gamepad gp = null) => IsLeftStickMovedUp(gp);
    public static bool IsRightStickReleased(Gamepad gp = null) => IsRightStickMovedUp(gp);

    // ========= Face Buttons =========
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

    // ========= Shoulders / Triggers =========
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

    // ========= Start / Select =========
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

    // ========= Sticks moved (アナログの動き判定：生) =========
    public static bool IsLeftStickMoved(float threshold = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.leftStick.ReadValue().sqrMagnitude ?? 0f) >= threshold * threshold;
    public static bool IsRightStickMoved(float threshold = DefaultStickThreshold, Gamepad gp = null)
        => (GP(gp)?.rightStick.ReadValue().sqrMagnitude ?? 0f) >= threshold * threshold;

    // 方向別（簡易：生値）
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

    // ========= Stick diagonals (8-way helpers) =========
    public static bool IsLeftStickRU(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.leftStick.ReadValue();
        return v.x >= th && v.y >= th;
    }
    public static bool IsLeftStickLU(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.leftStick.ReadValue();
        return v.x <= -th && v.y >= th;
    }
    public static bool IsLeftStickRD(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.leftStick.ReadValue();
        return v.x >= th && v.y <= -th;
    }
    public static bool IsLeftStickLD(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.leftStick.ReadValue();
        return v.x <= -th && v.y <= -th;
    }
    public static bool IsRightStickRU(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.rightStick.ReadValue();
        return v.x >= th && v.y >= th;
    }
    public static bool IsRightStickLU(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.rightStick.ReadValue();
        return v.x <= -th && v.y >= th;
    }
    public static bool IsRightStickRD(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.rightStick.ReadValue();
        return v.x >= th && v.y <= -th;
    }
    public static bool IsRightStickLD(float th = DefaultStickThreshold, Gamepad gp = null)
    {
        var g = GP(gp);
        if (g == null) return false;
        Vector2 v = g.rightStick.ReadValue();
        return v.x <= -th && v.y <= -th;
    }

    // ========= 方向まとめ（既存：Any = L/R Stick + DPad 統合） =========
    private struct DirState
    {
        public int Frame;
        public bool HeldR, HeldL, HeldU, HeldD;
        public bool DownR, DownL, DownU, DownD;
        public bool UpR, UpL, UpU, UpD;
    }
    private static readonly Dictionary<int, DirState> s_DirStates = new Dictionary<int, DirState>();

    private static void EnsureDirState(Gamepad g)
    {
        if (g == null) return;

        int id = g.deviceId;
        s_DirStates.TryGetValue(id, out var st);
        if (st.Frame == Time.frameCount) return; // 今フレーム更新済

        // 前フレームの保持
        bool prevR = st.HeldR, prevL = st.HeldL, prevU = st.HeldU, prevD = st.HeldD;

        // 現在のHeld（アナログは Schmitt ラッチ値で安定化）
        var ss = EnsureSchmitt(g);
        bool analogR = (ss.Lx > 0) || (ss.Rx > 0);
        bool analogL = (ss.Lx < 0) || (ss.Rx < 0);
        bool analogU = (ss.Ly > 0) || (ss.Ry > 0);
        bool analogD = (ss.Ly < 0) || (ss.Ry < 0);

        bool dpadR = g.dpad.right.isPressed;
        bool dpadL = g.dpad.left.isPressed;
        bool dpadU = g.dpad.up.isPressed;
        bool dpadD = g.dpad.down.isPressed;

        st.HeldR = analogR || dpadR;
        st.HeldL = analogL || dpadL;
        st.HeldU = analogU || dpadU;
        st.HeldD = analogD || dpadD;

        // Down（D-PadのwasPressedThisFrame も OR）
        st.DownR = (g.dpad.right.wasPressedThisFrame) || (st.HeldR && !prevR);
        st.DownL = (g.dpad.left.wasPressedThisFrame) || (st.HeldL && !prevL);
        st.DownU = (g.dpad.up.wasPressedThisFrame) || (st.HeldU && !prevU);
        st.DownD = (g.dpad.down.wasPressedThisFrame) || (st.HeldD && !prevD);

        // Up（D-PadのwasReleasedThisFrame も OR）
        st.UpR = (g.dpad.right.wasReleasedThisFrame) || (!st.HeldR && prevR);
        st.UpL = (g.dpad.left.wasReleasedThisFrame) || (!st.HeldL && prevL);
        st.UpU = (g.dpad.up.wasReleasedThisFrame) || (!st.HeldU && prevU);
        st.UpD = (g.dpad.down.wasReleasedThisFrame) || (!st.HeldD && prevD);

        st.Frame = Time.frameCount;
        s_DirStates[id] = st;
    }

    private static bool TryGetDirState(Gamepad gp, out DirState st)
    {
        var g = GP(gp);
        if (g == null) { st = default; return false; }
        EnsureDirState(g);
        st = s_DirStates[g.deviceId];
        return true;
    }

    // ---- 既存：Any（統合）の Right/Left/Up/Down ----
    public static bool IsRightDown(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.DownR;
    public static bool IsRightHeld(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.HeldR;
    public static bool IsRightUp(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.UpR;
    public static bool IsRight(Gamepad gp = null) => IsRightHeld(gp);

    public static bool IsLeftDown(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.DownL;
    public static bool IsLeftHeld(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.HeldL;
    public static bool IsLeftUp(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.UpL;
    public static bool IsLeft(Gamepad gp = null) => IsLeftHeld(gp);

    public static bool IsUpDown(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.DownU;
    public static bool IsUpHeld(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.HeldU;
    public static bool IsUpUp(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.UpU;
    public static bool IsUp(Gamepad gp = null) => IsUpHeld(gp);

    public static bool IsDownDown(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.DownD;
    public static bool IsDownHeld(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.HeldD;
    public static bool IsDownUp(Gamepad gp = null) => TryGetDirState(gp, out var st) && st.UpD;
    public static bool IsDown(Gamepad gp = null) => IsDownHeld(gp);

    // ========= ここから追記：入力ソース選択版 =========

    // 方向入力のソース選択
    public enum DirInputSource
    {
        Any = 0,     // 既存と同じ（LStick or RStick or DPad の統合）
        LStick = 1,  // 左スティックのみ
        RStick = 2,  // 右スティックのみ
        DPad = 3,   // D-Padのみ
        Sticks = 4,  // LStick or RStick（D-Pad除外）

        // エイリアス
        L = LStick,
        R = RStick,
    }

    // ソース別の前回Held（エッジ生成用）
    private struct DirPrevBySource
    {
        public int Frame;
        public bool LS_R, LS_L, LS_U, LS_D;
        public bool RS_R, RS_L, RS_U, RS_D;
        public bool DP_R, DP_L, DP_U, DP_D;
    }
    private static readonly Dictionary<int, DirPrevBySource> s_PrevSrc = new Dictionary<int, DirPrevBySource>();

    // 今フレームのソース別 Held/Down/Up をキャッシュ
    private struct DirStateBySource
    {
        public int Frame;

        // Left Stick
        public bool LS_HR, LS_HL, LS_HU, LS_HD;
        public bool LS_DR, LS_DL, LS_DU, LS_DD;
        public bool LS_UR, LS_UL, LS_UU, LS_UD;

        // Right Stick
        public bool RS_HR, RS_HL, RS_HU, RS_HD;
        public bool RS_DR, RS_DL, RS_DU, RS_DD;
        public bool RS_UR, RS_UL, RS_UU, RS_UD;

        // D-Pad
        public bool DP_HR, DP_HL, DP_HU, DP_HD;
        public bool DP_DR, DP_DL, DP_DU, DP_DD;
        public bool DP_UR, DP_UL, DP_UU, DP_UD;
    }
    private static readonly Dictionary<int, DirStateBySource> s_StateSrc = new Dictionary<int, DirStateBySource>();

    private static DirStateBySource EnsureDirStateBySource(Gamepad g)
    {
        int id = g.deviceId;
        s_StateSrc.TryGetValue(id, out var st);
        if (st.Frame == Time.frameCount) return st;

        s_PrevSrc.TryGetValue(id, out var prev);

        // 1) 現在の Held（L/Rスティックは Schmitt ラッチ、D-Pad はデジタル）
        var ss = EnsureSchmitt(g);

        bool lsR = (ss.Lx > 0), lsL = (ss.Lx < 0), lsU = (ss.Ly > 0), lsD = (ss.Ly < 0);
        bool rsR = (ss.Rx > 0), rsL = (ss.Rx < 0), rsU = (ss.Ry > 0), rsD = (ss.Ry < 0);

        bool dpR = g.dpad.right.isPressed, dpL = g.dpad.left.isPressed;
        bool dpU = g.dpad.up.isPressed, dpD = g.dpad.down.isPressed;

        st.LS_HR = lsR; st.LS_HL = lsL; st.LS_HU = lsU; st.LS_HD = lsD;
        st.RS_HR = rsR; st.RS_HL = rsL; st.RS_HU = rsU; st.RS_HD = rsD;
        st.DP_HR = dpR; st.DP_HL = dpL; st.DP_HU = dpU; st.DP_HD = dpD;

        // 2) Down（0→1）
        st.LS_DR = lsR && !prev.LS_R; st.LS_DL = lsL && !prev.LS_L;
        st.LS_DU = lsU && !prev.LS_U; st.LS_DD = lsD && !prev.LS_D;

        st.RS_DR = rsR && !prev.RS_R; st.RS_DL = rsL && !prev.RS_L;
        st.RS_DU = rsU && !prev.RS_U; st.RS_DD = rsD && !prev.RS_D;

        // D-Pad は wasPressedThisFrame を OR（チャタ抑止）
        st.DP_DR = g.dpad.right.wasPressedThisFrame || (dpR && !prev.DP_R);
        st.DP_DL = g.dpad.left.wasPressedThisFrame || (dpL && !prev.DP_L);
        st.DP_DU = g.dpad.up.wasPressedThisFrame || (dpU && !prev.DP_U);
        st.DP_DD = g.dpad.down.wasPressedThisFrame || (dpD && !prev.DP_D);

        // 3) Up（1→0）
        st.LS_UR = !lsR && prev.LS_R; st.LS_UL = !lsL && prev.LS_L;
        st.LS_UU = !lsU && prev.LS_U; st.LS_UD = !lsD && prev.LS_D;

        st.RS_UR = !rsR && prev.RS_R; st.RS_UL = !rsL && prev.RS_L;
        st.RS_UU = !rsU && prev.RS_U; st.RS_UD = !rsD && prev.RS_D;

        // D-Pad は wasReleasedThisFrame を OR
        st.DP_UR = g.dpad.right.wasReleasedThisFrame || (!dpR && prev.DP_R);
        st.DP_UL = g.dpad.left.wasReleasedThisFrame || (!dpL && prev.DP_L);
        st.DP_UU = g.dpad.up.wasReleasedThisFrame || (!dpU && prev.DP_U);
        st.DP_UD = g.dpad.down.wasReleasedThisFrame || (!dpD && prev.DP_D);

        st.Frame = Time.frameCount;
        s_StateSrc[id] = st;

        // 前回値更新
        prev.LS_R = lsR; prev.LS_L = lsL; prev.LS_U = lsU; prev.LS_D = lsD;
        prev.RS_R = rsR; prev.RS_L = rsL; prev.RS_U = rsU; prev.RS_D = rsD;
        prev.DP_R = dpR; prev.DP_L = dpL; prev.DP_U = dpU; prev.DP_D = dpD;
        prev.Frame = Time.frameCount;
        s_PrevSrc[id] = prev;

        return st;
    }

    private static bool TryGetDirState(Gamepad gp, DirInputSource src, out DirStateBySource st)
    {
        var g = GP(gp);
        if (g == null) { st = default; return false; }
        st = EnsureDirStateBySource(g);
        return true;
    }

    private static (bool R, bool L, bool U, bool D) SelectHeld(DirStateBySource s, DirInputSource src)
    {
        return src switch
        {
            DirInputSource.LStick => (s.LS_HR, s.LS_HL, s.LS_HU, s.LS_HD),
            DirInputSource.RStick => (s.RS_HR, s.RS_HL, s.RS_HU, s.RS_HD),
            DirInputSource.DPad => (s.DP_HR, s.DP_HL, s.DP_HU, s.DP_HD),
            DirInputSource.Sticks => (s.LS_HR || s.RS_HR, s.LS_HL || s.RS_HL, s.LS_HU || s.RS_HU, s.LS_HD || s.RS_HD),
            _ /* Any */           => (
                s.LS_HR || s.RS_HR || s.DP_HR,
                s.LS_HL || s.RS_HL || s.DP_HL,
                s.LS_HU || s.RS_HU || s.DP_HU,
                s.LS_HD || s.RS_HD || s.DP_HD
            ),
        };
    }
    private static (bool R, bool L, bool U, bool D) SelectDown(DirStateBySource s, DirInputSource src)
    {
        return src switch
        {
            DirInputSource.LStick => (s.LS_DR, s.LS_DL, s.LS_DU, s.LS_DD),
            DirInputSource.RStick => (s.RS_DR, s.RS_DL, s.RS_DU, s.RS_DD),
            DirInputSource.DPad => (s.DP_DR, s.DP_DL, s.DP_DU, s.DP_DD),
            DirInputSource.Sticks => (s.LS_DR || s.RS_DR, s.LS_DL || s.RS_DL, s.LS_DU || s.RS_DU, s.LS_DD || s.RS_DD),
            _ /* Any */           => (
                s.LS_DR || s.RS_DR || s.DP_DR,
                s.LS_DL || s.RS_DL || s.DP_DL,
                s.LS_DU || s.RS_DU || s.DP_DU,
                s.LS_DD || s.RS_DD || s.DP_DD
            ),
        };
    }
    private static (bool R, bool L, bool U, bool D) SelectUp(DirStateBySource s, DirInputSource src)
    {
        return src switch
        {
            DirInputSource.LStick => (s.LS_UR, s.LS_UL, s.LS_UU, s.LS_UD),
            DirInputSource.RStick => (s.RS_UR, s.RS_UL, s.RS_UU, s.RS_UD),
            DirInputSource.DPad => (s.DP_UR, s.DP_UL, s.DP_UU, s.DP_UD),
            DirInputSource.Sticks => (s.LS_UR || s.RS_UR, s.LS_UL || s.RS_UL, s.LS_UU || s.RS_UU, s.LS_UD || s.RS_UD),
            _ /* Any */           => (
                s.LS_UR || s.RS_UR || s.DP_UR,
                s.LS_UL || s.RS_UL || s.DP_UL,
                s.LS_UU || s.RS_UU || s.DP_UU,
                s.LS_UD || s.RS_UD || s.DP_UD
            ),
        };
    }

    // ---- ソース選択版の Right/Left/Up/Down ----
    public static bool IsRightDown(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectDown(st, src).R;
    public static bool IsRightHeld(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectHeld(st, src).R;
    public static bool IsRightUp(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectUp(st, src).R;
    public static bool IsRight(DirInputSource src, Gamepad gp = null)
        => IsRightHeld(src, gp);

    public static bool IsLeftDown(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectDown(st, src).L;
    public static bool IsLeftHeld(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectHeld(st, src).L;
    public static bool IsLeftUp(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectUp(st, src).L;
    public static bool IsLeft(DirInputSource src, Gamepad gp = null)
        => IsLeftHeld(src, gp);

    public static bool IsUpDown(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectDown(st, src).U;
    public static bool IsUpHeld(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectHeld(st, src).U;
    public static bool IsUpUp(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectUp(st, src).U;
    public static bool IsUp(DirInputSource src, Gamepad gp = null)
        => IsUpHeld(src, gp);

    public static bool IsDownDown(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectDown(st, src).D;
    public static bool IsDownHeld(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectHeld(st, src).D;
    public static bool IsDownUp(DirInputSource src, Gamepad gp = null)
        => TryGetDirState(gp, src, out var st) && SelectUp(st, src).D;
    public static bool IsDown(DirInputSource src, Gamepad gp = null)
        => IsDownHeld(src, gp);

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
            g.leftTrigger.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame ||
            // 方向 Down も含めたい場合は以下をOR
            IsRightDown(g) || IsLeftDown(g) || IsUpDown(g) || IsDownDown(g);
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
            g.leftTrigger.isPressed || g.rightTrigger.isPressed ||
            // 方向 Held も含める
            IsRightHeld(g) || IsLeftHeld(g) || IsUpHeld(g) || IsDownHeld(g);
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
            g.leftTrigger.wasReleasedThisFrame || g.rightTrigger.wasReleasedThisFrame ||
            // 方向 Up も含める
            IsRightUp(g) || IsLeftUp(g) || IsUpUp(g) || IsDownUp(g);
    }
}
