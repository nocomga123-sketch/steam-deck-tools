using System;
using WindowsInput;

namespace SteamController.Profiles.Default
{
    public abstract class ShortcutsProfile : Profile
    {
        public const String ShortcutConsumed = "ShortcutsProfile";
        public readonly TimeSpan HoldForShorcuts = TimeSpan.FromMilliseconds(200);
        
        // Giữ nguyên cấu hình thời gian 10 giây và 15 giây của bạn
        private readonly TimeSpan HoldToSwitchProfile = TimeSpan.FromSeconds(5);
        private readonly TimeSpan HoldToSwitchDesktop = TimeSpan.FromSeconds(7);

        public override Status Run(Context c)
        {
            // Steam + 3 dots simulate CTRL+SHIFT+ESCAPE (Mở Task Manager)
            if (c.Steam.BtnSteam.Hold(HoldForShorcuts, ShortcutConsumed) && c.Steam.BtnQuickAccess.HoldOnce(HoldForShorcuts, ShortcutConsumed))
            {
                c.Keyboard.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.SHIFT }, VirtualKeyCode.ESCAPE);
                return Status.Done;
            }

            // Đảo mốc 15 giây (HoldChain) lên trước mốc 10 giây (HoldOnce) của nút Options (3 vạch)
            if (c.Steam.BtnOptions.HoldChain(HoldToSwitchDesktop, ShortcutConsumed, "SwitchToDesktop"))
            {
                c.BackToDefault();
                return Status.Done;
            }
            else if (c.Steam.BtnOptions.HoldOnce(HoldToSwitchProfile, ShortcutConsumed))
            {
                if (!c.SelectNext())
                    c.BackToDefault();
                return Status.Done;
            }

            // Luôn tiêu thụ nút 3 chấm (Quick Access) để tránh bị nhận diện nhầm lệnh khác
            if (c.Steam.BtnQuickAccess.Hold(HoldForShorcuts, ShortcutConsumed))
            {
                return Status.Done;
            }

            // Tổ hợp phím giữ nút Steam + Nút khác
            if (c.Steam.BtnSteam.Hold(HoldForShorcuts, ShortcutConsumed))
            {
                if (SteamShortcuts(c))
                {
                    return Status.Done;
                }
            }

            return Status.Continue;
        }

        protected virtual bool SteamShortcuts(Context c)
        {
            // Nút Steam + Options (Nút 3 vạch) -> Mở Task View (Win + Tab)
            if (c.Steam.BtnOptions.Pressed())
            {
                c.Keyboard.KeyPress(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
                return true;
            }

            // Nút Steam + Menu (Nút 2 ô vuông) -> Bật/Tắt Toàn màn hình (F11)
            if (c.Steam.BtnMenu.Pressed())
            {
                c.Keyboard.KeyPress(VirtualKeyCode.F11);
                return true;
            }

            return false;
        }
    }
}
