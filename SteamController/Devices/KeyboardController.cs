using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput;

namespace SteamController.Devices
{
    public class KeyboardController : IDisposable
    {
        InputSimulator simulator = new InputSimulator();

        // Chỉ cần lưu danh sách các phím đang được nhấn (Không cần lưu DateTime phức tạp nữa)
        HashSet<VirtualKeyCode> keyCodes = new HashSet<VirtualKeyCode>();
        HashSet<VirtualKeyCode> lastKeyCodes = new HashSet<VirtualKeyCode>();

        public KeyboardController()
        {
        }

        public void Dispose()
        {
        }

        public bool this[System.Windows.Forms.Keys key]
        {
            get
            {
                if (key.HasFlag(System.Windows.Forms.Keys.Shift) && !this[VirtualKeyCode.SHIFT])
                    return false;
                if (key.HasFlag(System.Windows.Forms.Keys.Alt) && !this[VirtualKeyCode.MENU])
                    return false;
                if (key.HasFlag(System.Windows.Forms.Keys.Control) && !this[VirtualKeyCode.CONTROL])
                    return false;
                return this[(VirtualKeyCode)(key & System.Windows.Forms.Keys.KeyCode)];
            }
            set
            {
                if (value)
                {
                    this[VirtualKeyCode.SHIFT] = key.HasFlag(System.Windows.Forms.Keys.Shift);
                    this[VirtualKeyCode.MENU] = key.HasFlag(System.Windows.Forms.Keys.Alt);
                    this[VirtualKeyCode.CONTROL] = key.HasFlag(System.Windows.Forms.Keys.Control);
                    this[(VirtualKeyCode)(key & System.Windows.Forms.Keys.KeyCode)] = true;
                }
            }
        }

        public bool this[VirtualKeyCode key]
        {
            get { return keyCodes.Contains(key); }
            set
            {
                if (key == VirtualKeyCode.None)
                    return;

                if (value)
                {
                    keyCodes.Add(key);
                }
                else
                {
                    keyCodes.Remove(key);
                }
            }
        }

        public VirtualKeyCode[] DownKeys
        {
            get { return keyCodes.ToArray(); }
        }

        internal void BeforeUpdate()
        {
            // Lưu lại trạng thái của frame trước để so sánh
            lastKeyCodes = new HashSet<VirtualKeyCode>(keyCodes);
            keyCodes.Clear(); // Xóa sạch để nhận dữ liệu mới từ tay cầm ở frame này
        }

        private void Safe(Action action)
        {
            try
            {
                action();
                Managers.SASManager.Valid = true;
            }
            catch (InvalidOperationException)
            {
                Managers.SASManager.Valid = false;
            }
        }

        internal void Update()
        {
            // 1. KEY UP: Nếu frame trước có mà frame này KHÔNG CÓ -> Thả phím ra ngay lập tức
            var keysUp = lastKeyCodes.Except(keyCodes).ToArray();
            foreach (var key in keysUp)
            {
                Safe(() => simulator.Keyboard.KeyUp(key));
            }

            // 2. KEY DOWN: Nếu frame trước KHÔNG CÓ mà frame này CÓ -> Đè phím xuống
            var keysDown = keyCodes.Except(lastKeyCodes).ToArray();
            foreach (var key in keysDown)
            {
                Safe(() => simulator.Keyboard.KeyDown(key));
            }

            // XÓA BỎ HOÀN TOÀN ĐOẠN CODE "KEY REPEATS" GÂY LỖI KẸT PHÍM/NHẤP NHẢ
        }

        public void Overwrite(VirtualKeyCode key, bool value)
        {
            if (value)
                keyCodes.Add(key);
            else
                keyCodes.Remove(key);
        }

        public void KeyPress(params VirtualKeyCode[] keyCodes)
        {
            Safe(() => simulator.Keyboard.KeyPress(keyCodes));
        }

        public void KeyPress(VirtualKeyCode modifierKey, params VirtualKeyCode[] keyCodes)
        {
            Safe(() => simulator.Keyboard.ModifiedKeyStroke(modifierKey, keyCodes));
        }

        public void KeyPress(IEnumerable<VirtualKeyCode> modifierKeys, params VirtualKeyCode[] keyCodes)
        {
            Safe(() => simulator.Keyboard.ModifiedKeyStroke(modifierKeys, keyCodes));
        }
    }
}
