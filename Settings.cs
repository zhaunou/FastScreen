using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    sealed class Shortcut
    {
        public readonly Keys Key;
        public readonly uint Modifiers;
        public Shortcut(Keys key, uint modifiers) { Key = key; Modifiers = modifiers; }
        public bool Valid
        {
            get { return (Modifiers & 3) != 0 && (Modifiers & ~7u) == 0 &&
                ((Key >= Keys.A && Key <= Keys.Z) || (Key >= Keys.D0 && Key <= Keys.D9) ||
                (Key >= Keys.F1 && Key <= Keys.F11) || (Key >= Keys.F13 && Key <= Keys.F24) || Key == Keys.PrintScreen); }
        }
        public static Shortcut FromKeys(Keys data)
        {
            return new Shortcut(data & Keys.KeyCode, (uint)(((data & Keys.Alt) != 0 ? 1 : 0) |
                ((data & Keys.Control) != 0 ? 2 : 0) | ((data & Keys.Shift) != 0 ? 4 : 0)));
        }
        public string Encode() { return Modifiers + ":" + (int)Key; }
        public static Shortcut Decode(string value)
        {
            string[] parts = value.Split(':'); uint modifiers; int key;
            if (parts.Length != 2 || !uint.TryParse(parts[0], out modifiers) || !int.TryParse(parts[1], out key)) return null;
            Shortcut result = new Shortcut((Keys)key, modifiers);
            return result.Valid ? result : null;
        }
        public override string ToString()
        {
            return ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") +
                ((Modifiers & 4) != 0 ? "Shift + " : "") + (Key >= Keys.D0 && Key <= Keys.D9 ? ((int)Key - (int)Keys.D0).ToString() : Key.ToString());
        }
    }

    sealed class Preferences
    {
        public Shortcut Full = new Shortcut(Keys.F, 6);
        public Shortcut Region = new Shortcut(Keys.A, 6);
        public static Preferences Read(string file)
        {
            Preferences prefs = new Preferences();
            string text;
            if (SettingsStorage.TryRead(file, SettingsStorage.HotkeyLimit, out text))
            {
                using (StringReader reader = new StringReader(text))
                {
                    string first = reader.ReadLine(), second = reader.ReadLine();
                    if (first != null && second != null && reader.ReadLine() == null)
                    {
                        Shortcut full = Shortcut.Decode(first), region = Shortcut.Decode(second);
                        if (full != null && region != null && full.Encode() != region.Encode()) { prefs.Full = full; prefs.Region = region; }
                    }
                }
            }
            return prefs;
        }
        public void Save(string file)
        {
            if (Full == null || Region == null || !Full.Valid || !Region.Valid || Full.Encode() == Region.Encode())
                throw new ArgumentException("Для двух режимов нужны разные корректные сочетания.");
            SettingsStorage.WriteAtomic(file, Full.Encode() + "\r\n" + Region.Encode() + "\r\n", SettingsStorage.HotkeyLimit);
        }
    }

    sealed class GlobalShortcuts : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
        readonly IntPtr window;
        public bool FullActive { get; private set; }
        public bool RegionActive { get; private set; }
        public GlobalShortcuts(IntPtr handle) { window = handle; }
        public void Register(Preferences prefs)
        {
            Dispose();
            FullActive = RegisterHotKey(window, 1, prefs.Full.Modifiers | 0x4000, (uint)prefs.Full.Key);
            RegionActive = RegisterHotKey(window, 2, prefs.Region.Modifiers | 0x4000, (uint)prefs.Region.Key);
        }
        public bool TryApply(Preferences next, Preferences previous, out string error)
        {
            Register(next);
            if (FullActive && RegionActive) { error = null; return true; }
            error = !FullActive ? "Сочетание для всего экрана занято или недоступно." : "Сочетание для области занято или недоступно.";
            Register(previous);
            return false;
        }
        public void Dispose()
        {
            if (FullActive) UnregisterHotKey(window, 1);
            if (RegionActive) UnregisterHotKey(window, 2);
            FullActive = RegionActive = false;
        }
    }
}
