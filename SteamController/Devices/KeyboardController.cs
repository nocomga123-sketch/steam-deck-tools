using System;
using System.Collections.Generic;
using System.Linq; // <-- Thêm dòng này để chạy được hàm Update mới
using WindowsInput;

namespace SteamController.Devices
{
    public class KeyboardController : IDisposable
    {
        public static readonly TimeSpan FirstRepeat = TimeSpan.FromMilliseconds(400);
        public static readonly TimeSpan NextRepeats = TimeSpan.FromMilliseconds(45);

        InputSimulator simulator = new InputSimulator();

        Dictionary<VirtualKeyCode, DateTime> keyCodes = new Dictionary<VirtualKeyCode, DateTime>();
        Dictionary<VirtualKeyCode, DateTime> lastKeyCodes = new Dictionary<VirtualKeyCode, DateTime>();

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
            get { return keyCodes.ContainsKey(key); }
            set
            {
                if (key == VirtualKeyCode.None)
                    return;

                if (value)
                {
                    if (keyCodes.ContainsKey(key))
                        return;
                    if (!lastKeyCodes.TryGetValue(key, out var keyRepeat))
                        keyRepeat = DateTime.Now.Add(FirstRepeat);
                    keyCodes.Add(key, keyRepeat);
                }
                else
                {
                    // Giải phóng phím khi truyền vào false
                    keyCodes.Remove(key);
                }
            }
        }

        public VirtualKeyCode[] DownKeys
        {
            get { return keyCodes.Keys.ToArray(); }
        }

        internal void BeforeUpdate()
        {
            lastKeyCodes = keyCodes;
            keyCodes = new Dictionary<VirtualKeyCode, DateTime>();
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

        // HÀM UPDATE ĐÃ ĐƯỢC SỬA TOÀN BỘ LỖI NHẤP NHẢ VÀ LỖI ENUMERATION
        internal void Update()
        {
            var now = DateTime.Now;

            // 1. KEY UP: Chờ 60ms để lọc bớt tín hiệu nhiễu/trễ từ nút phụ L4/L5/R4/R5
            foreach (var keyUp in lastKeyCodes)
            {
                if (!keyCodes.ContainsKey(keyUp.Key))
                {
                    if (now - keyUp.Value > TimeSpan.FromMilliseconds(60)) 
                    {
                        Safe(() => simulator.Keyboard.KeyUp(keyUp.Key));
                    }
                    else
                    {
                        keyCodes[keyUp.Key] = keyUp.Value; 
                    }
                }
            }

            // 2. KEY DOWN: Nhấn phím xuống
            foreach (var keyDown in keyCodes.Except(lastKeyCodes))
            {
                Safe(() => simulator.Keyboard.KeyDown(keyDown.Key));
            }

            // 3. KEY REPEATS: Nhấn giữ phím liên tục mượt mà
            var keysToRepeat = keyCodes.Where(keyPress => keyPress.Value <= now)
                                       .Select(keyPress => keyPress.Key)
                                       .ToArray();

            foreach (var key in keysToRepeat)
            {
                Safe(() => simulator.Keyboard.KeyPress(key));
                keyCodes[key] = DateTime.Now.Add(NextRepeats);
            }
        }

        public void Overwrite(VirtualKeyCode key, bool value)
        {
            if (value)
                this[key] = true;
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
