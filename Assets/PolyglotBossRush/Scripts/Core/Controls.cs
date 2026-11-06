using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace PolyglotBossRush
{
    // 画面上のボタン（移動の十字キー、巻き戻し、リセット）と、キーボード・ゲームパッドの入力をまとめる。
    // ボタンは IMGUI で描き、押されているかは Input System のタッチとマウスで判定する（マルチタッチ対応）
    public sealed class Controls
    {
        const float ButtonSize = 84f;

        public Vector2 Direction { get; private set; }
        public bool RewindHeld { get; private set; }
        public bool RewindStarted { get; private set; }
        public bool ResetPressed { get; private set; }
        /// <summary>このフレームに何か操作されたか（デモを止めて手動に切り替えるため）</summary>
        public bool AnyInput { get; private set; }

        Rect up, down, left, right, rewind, reset;
        bool upHeld, downHeld, leftHeld, rightHeld, resetHeld;
        float scale = 1f;

        /// <summary>HUD と同じ拡大率。540x960 を基準にする</summary>
        public static float GuiScale => Mathf.Min(Screen.width / 540f, Screen.height / 960f);

        public Controls() => EnhancedTouchSupport.Enable();

        public void Update()
        {
            Layout();
            upHeld = downHeld = leftHeld = rightHeld = false;
            var rewindNow = false;
            var resetNow = false;
            var touched = false;
            foreach (var point in Pointers())
            {
                touched = true;
                upHeld |= up.Contains(point);
                downHeld |= down.Contains(point);
                leftHeld |= left.Contains(point);
                rightHeld |= right.Contains(point);
                rewindNow |= rewind.Contains(point);
                resetNow |= reset.Contains(point);
            }

            var direction = new Vector2((rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f), (upHeld ? 1f : 0f) - (downHeld ? 1f : 0f));
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction.x -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction.x += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) direction.y -= 1f;
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) direction.y += 1f;
                rewindNow |= keyboard.zKey.isPressed;
                resetNow |= keyboard.rKey.isPressed;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                direction += gamepad.leftStick.ReadValue();
                rewindNow |= gamepad.leftTrigger.isPressed;
                resetNow |= gamepad.startButton.isPressed;
            }
            Direction = Vector2.ClampMagnitude(direction, 1f);
            AnyInput = touched || rewindNow || resetNow || Direction != Vector2.zero;
            RewindStarted = rewindNow && !RewindHeld;
            RewindHeld = rewindNow;
            ResetPressed = resetNow && !resetHeld;
            resetHeld = resetNow;
        }

        // 画面の左下に十字キー、右下に巻き戻しとリセット（IMGUI の座標系: 左上原点、拡大前）
        void Layout()
        {
            scale = GuiScale;
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var center = new Vector2(24f + ButtonSize * 1.5f, height - 24f - ButtonSize * 1.5f);
            up = Square(center + new Vector2(0f, -ButtonSize));
            down = Square(center + new Vector2(0f, ButtonSize));
            left = Square(center + new Vector2(-ButtonSize, 0f));
            right = Square(center + new Vector2(ButtonSize, 0f));
            rewind = new Rect(width - 24f - 180f, height - 24f - ButtonSize * 2f - 12f, 180f, ButtonSize * 2f - 40f);
            reset = new Rect(width - 24f - 180f, height - 24f - 52f, 180f, 52f);
        }

        static Rect Square(Vector2 center) => new Rect(center.x - ButtonSize / 2f, center.y - ButtonSize / 2f, ButtonSize - 6f, ButtonSize - 6f);

        // 押されているタッチとマウスの位置を、IMGUI の座標系で返す
        System.Collections.Generic.IEnumerable<Vector2> Pointers()
        {
            // EnhancedTouch はタッチをイベント単位で追うので、1 フレームの中で押して離した短いタップも
            // 「このフレームで終わったタッチ」として残る（Touchscreen の押下状態を見るだけでは取りこぼす）
            foreach (var touch in EnhancedTouch.activeTouches)
                yield return ToGui(touch.screenPosition);
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame))
                yield return ToGui(Mouse.current.position.ReadValue());
        }

        Vector2 ToGui(Vector2 screen) => new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);

        public void Draw(bool rewindAvailable)
        {
            DrawButton(up, "^", upHeld);
            DrawButton(down, "v", downHeld);
            DrawButton(left, "<", leftHeld);
            DrawButton(right, ">", rightHeld);
            DrawButton(rewind, rewindAvailable ? "<< REWIND" : "REWIND\n(unavailable)", RewindHeld, rewindAvailable);
            DrawButton(reset, "RESET", resetHeld);
        }

        static void DrawButton(Rect rect, string label, bool held, bool enabled = true)
        {
            var color = GUI.color;
            GUI.color = !enabled ? new Color(1f, 1f, 1f, 0.25f) : held ? new Color(0.6f, 1f, 0.8f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUI.Box(rect, label, style);
            GUI.color = color;
        }
    }
}
