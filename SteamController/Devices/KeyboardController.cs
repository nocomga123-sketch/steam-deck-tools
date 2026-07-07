using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput;

namespace SteamController.Devices
{
    public class KeyboardController : IDisposable
    {
        public static readonly TimeSpan FirstRepeat = TimeSpan.FromMilliseconds(400);
        public static readonly TimeSpan NextRepeats = TimeSpan.FromMilliseconds(45);
        
        // Thời gian chờ tối đa cho các nút phụ L4/L5 (60 mili-giây)
        private static readonly TimeSpan MissingTimeout = TimeSpan.FromMilliseconds(60);

        InputSimulator simulator = new InputSimulator();

        Dictionary<VirtualKeyCode, DateTime> keyCodes = new Dictionary<VirtualKeyCode, DateTime>();
        Dictionary<VirtualKeyCode, DateTime> lastKeyCodes = new Dictionary<VirtualKeyCode, DateTime>();
        
        // Danh sách mới: Lưu thời điểm mà một phím bắt đầu bị mất tín hiệu từ tay cầm
        Dictionary<VirtualKeyCode, DateTime> missingKeys = new Dictionary<VirtualKeyCode, DateTime>();

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

        internal void Update()
        {
            var now = DateTime.Now;

            // BƯỚC 1: XỬ LÝ LỌC NHIỄU CHO NÚT L4/L5 KHI BỊ MẤT TÍN HIỆU GIỮA CÁC FRAME
            // Duyệt qua các phím ở frame trước mà frame này tay cầm tạm thời không báo nhấn nữa
            foreach (var key in lastKeyCodes.Keys)
            {
                if (!keyCodes.ContainsKey(key))
                {
                    if (!missingKeys.ContainsKey(key))
                    {
                        // Đánh dấu thời điểm phím này bắt đầu bị mất tín hiệu
                        missingKeys[key] = now;
                    }

                    // Nếu thời gian mất tín hiệu vẫn nằm trong khoảng cho phép (dưới 60ms)
                    if (now - missingKeys[key] < MissingTimeout)
                    {
                        // "Cứu" phím này bằng cách đưa nó ngược trở lại vào danh sách đang nhấn của frame hiện tại
                        keyCodes[key] = lastKeyCodes[key];
                    }
                }
            }

            // Xóa khỏi danh sách missing những phím đã có tín hiệu lại bình thường
            var recoveredKeys = missingKeys.Keys.Where(k => keyCodes.ContainsKey(k) && now - missingKeys[k] < MissingTimeout).ToArray();
            foreach (var key in recoveredKeys)
            {
                missingKeys.Remove(key);
            }

            // BƯỚC 2: KEY UP (Thực sự thả phím)
            // Chỉ thả phím khi frame này không có VÀ nó đã vượt quá thời gian chờ 60ms
            var keysToUp = lastKeyCodes.Keys.Except(keyCodes.Keys).ToArray();
            foreach (var keyUp in keysToUp)
            {
                Safe(() => simulator.Keyboard.KeyUp(keyUp));
                missingKeys.Remove(keyUp); // Xóa hẳn khỏi bộ nhớ theo dõi
            }

            // BƯỚC 3: KEY DOWN (Nhấn phím xuống lần đầu)
            foreach (var keyDown in keyCodes.Except(lastKeyCodes))
            {
                Safe(() => simulator.Keyboard.KeyDown(keyDown.Key));
            }

            // BƯỚC 4: KEY REPEATS (Nhấn giữ lặp lại phím)
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
