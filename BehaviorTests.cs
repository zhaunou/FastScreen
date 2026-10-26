using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    static class BehaviorTests
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); }
        static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
        static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        public static void Run(string report)
        {
            string root = Path.Combine(Path.GetDirectoryName(report), "behavior-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Shortcut parsed = Shortcut.FromKeys(Keys.Control | Keys.Alt | Keys.Shift | Keys.D5);
                Check(parsed.Valid && parsed.ToString() == "Ctrl + Alt + Shift + 5", "shortcut input and display");
                Check(Shortcut.Decode(parsed.Encode()).Encode() == parsed.Encode(), "shortcut serialization");
                Check(!Shortcut.FromKeys(Keys.A).Valid && !Shortcut.FromKeys(Keys.Shift | Keys.A).Valid, "plain typing rejected");
                Check(!Shortcut.FromKeys(Keys.Control | Keys.F12).Valid && Shortcut.Decode("broken") == null, "reserved and malformed shortcuts rejected");
                string file = Path.Combine(root, "hotkeys.txt");
                Preferences prefs = new Preferences { Full = parsed, Region = Shortcut.FromKeys(Keys.Control | Keys.Alt | Keys.R) };
                prefs.Save(file);
                Check(Preferences.Read(file).Full.Encode() == parsed.Encode(), "settings survive reload");
                prefs.Full = Shortcut.FromKeys(Keys.Alt | Keys.F8); prefs.Save(file);
                Check(Preferences.Read(file).Full.Encode() == prefs.Full.Encode(), "atomic settings replacement");
                File.WriteAllLines(file, new string[] { parsed.Encode(), parsed.Encode() });
                Check(Preferences.Read(file).Full.Encode() == new Preferences().Full.Encode(), "duplicate persisted settings recover defaults");
                File.WriteAllText(file, "invalid");
                Check(Preferences.Read(file).Region.Encode() == new Preferences().Region.Encode(), "corrupt settings recover defaults");
                using (Form owner = new Form())
                using (Form blocker = new Form())
                using (GlobalShortcuts shortcuts = new GlobalShortcuts(owner.Handle))
                {
                    System.Collections.Generic.List<Keys> free = new System.Collections.Generic.List<Keys>();
                    for (Keys key = Keys.F1; key <= Keys.F11 && free.Count < 3; key++)
                        if (RegisterHotKey(blocker.Handle, 70, 7, (uint)key)) { UnregisterHotKey(blocker.Handle, 70); free.Add(key); }
                    Check(free.Count == 3, "three test hotkeys available");
                    Preferences previous = new Preferences { Full = new Shortcut(free[0], 7), Region = new Shortcut(free[1], 7) };
                    shortcuts.Register(previous);
                    Check(shortcuts.FullActive && shortcuts.RegionActive, "global registration");
                    Check(RegisterHotKey(blocker.Handle, 70, 7, (uint)free[2]), "conflict blocker registered");
                    try
                    {
                        string error;
                        Preferences next = new Preferences { Full = new Shortcut(free[2], 7), Region = previous.Region };
                        Check(!shortcuts.TryApply(next, previous, out error) && !String.IsNullOrEmpty(error), "occupied shortcut rejected");
                        Check(shortcuts.FullActive && shortcuts.RegionActive, "old shortcuts restored after conflict");
                        Check(!RegisterHotKey(blocker.Handle, 71, 7, (uint)free[0]), "restored shortcut belongs to application");
                        UnregisterHotKey(blocker.Handle, 70);
                        Check(shortcuts.TryApply(next, previous, out error), "free shortcut can be applied");
                        shortcuts.Dispose();
                        Check(RegisterHotKey(blocker.Handle, 71, 7, (uint)free[2]), "shortcuts released on exit");
                        UnregisterHotKey(blocker.Handle, 71);
                    }
                    finally { UnregisterHotKey(blocker.Handle, 70); UnregisterHotKey(blocker.Handle, 71); }
                }
                using (MainWindow window = new MainWindow())
                {
                    window.Show();
                    Panel welcome = (Panel)Field(window, "welcome"), workspace = (Panel)Field(window, "workspace"), settings = (Panel)Field(window, "settings");
                    Check(welcome.Visible && !workspace.Visible, "welcome shown at startup");
                    Render(window, Path.Combine(Path.GetDirectoryName(report), "welcome-preview.png"));
                    Call(window, "ShowPage", workspace);
                    Check(workspace.Visible && !welcome.Visible, "welcome to workspace navigation");
                    Render(window, Path.Combine(Path.GetDirectoryName(report), "workspace-preview.png"));
                    Call(window, "OpenSettings");
                    Check(settings.Visible && !workspace.Visible, "settings navigation");
                    Render(window, Path.Combine(Path.GetDirectoryName(report), "settings-preview.png"));
                    Call(window, "HideToTray");
                    Check(!window.Visible && !window.ShowInTaskbar && ((NotifyIcon)Field(window, "tray")).Visible, "minimize to tray");
                    Call(window, "RestoreWindow");
                    Check(window.Visible && window.ShowInTaskbar, "restore from tray");
                    window.Close();
                    Check(!window.IsDisposed && !window.Visible, "close button hides to tray");
                    ContextMenuStrip menu = (ContextMenuStrip)Field(window, "trayMenu");
                    menu.Items[menu.Items.Count - 1].PerformClick();
                    Check(window.IsDisposed, "explicit tray exit disposes application");
                }
                File.WriteAllText(report, "PASS: shortcut validation; settings round trip/replacement/recovery; Win32 global registration; conflict rollback; hotkey release; welcome/workspace/settings navigation; tray hide/restore; close-to-tray; rendered all three screens.\r\n");
            }
            finally { foreach (string path in Directory.GetFiles(root)) File.Delete(path); Directory.Delete(root); }
        }
        static void Render(Form form, string path)
        {
            using (Bitmap image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }

        public static void CaptureLifecycle(string report)
        {
            string root = Path.Combine(Path.GetDirectoryName(report), "capture-lifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using (MainWindow window = new MainWindow())
                {
                    window.Show();
                    window.GetType().GetField("folder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, root);
                    Call(window, "HideToTray");
                    Message hotkey = Message.Create(window.Handle, 0x0312, new IntPtr(1), IntPtr.Zero);
                    Call(window, "WndProc", hotkey);
                    Check((bool)Field(window, "capturing") && !window.Visible, "hotkey dispatch starts hidden capture");
                    Call(window, "CaptureNow", null, EventArgs.Empty);
                    Check(!(bool)Field(window, "capturing") && !window.Visible && !window.ShowInTaskbar, "tray capture stays in tray");
                    Check(Directory.GetFiles(root, "*.png").Length == 1, "tray capture saved PNG");
                    Call(window, "RestoreWindow");
                    Call(window, "BeginCapture", false);
                    Call(window, "CaptureNow", null, EventArgs.Empty);
                    Check(window.Visible && ((Panel)Field(window, "workspace")).Visible, "foreground capture restores workspace");
                    Check(Directory.GetFiles(root, "*.png").Length == 2, "foreground capture saved PNG");
                }
                File.AppendAllText(report, "PASS: WM_HOTKEY dispatch; capture while hidden in tray; hidden state preserved after save; foreground capture restores workspace; both PNG files saved.\r\n");
            }
            finally { foreach (string path in Directory.GetFiles(root)) File.Delete(path); Directory.Delete(root); }
        }
    }
}
