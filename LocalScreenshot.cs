using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

[assembly: System.Reflection.AssemblyTitle("FastScreen")]
[assembly: System.Reflection.AssemblyProduct("FastScreen")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace LocalScreenshot
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length != 0)
            {
                MessageBox.Show("Параметры командной строки не поддерживаются. Для проверок используйте отдельную тестовую сборку.", AppInfo.DisplayName);
                Environment.ExitCode = 2;
                return;
            }
            try
            {
                using (SingleInstanceGate instance = SingleInstanceGate.ForCurrentUser())
                {
                    if (!instance.IsPrimary)
                    {
                        if (!instance.NotifyPrimary()) MessageBox.Show("FastScreen уже запущен. Откройте его через значок в трее.", "FastScreen");
                        return;
                    }
                    using (MainWindow window = new MainWindow())
                    {
                        RegisteredWaitHandle activationWait = null;
                        window.Shown += delegate
                        {
                            activationWait = ThreadPool.RegisterWaitForSingleObject(instance.Activation, delegate
                            {
                                try
                                {
                                    if (!window.IsDisposed && window.IsHandleCreated)
                                        window.BeginInvoke((MethodInvoker)window.RequestActivation);
                                }
                                catch (InvalidOperationException) { } // The window may be closing.
                            }, null, Timeout.Infinite, false);
                        };
                        try { Application.Run(window); }
                        finally { if (activationWait != null) activationWait.Unregister(null); }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить FastScreen.\n" + ex.Message, "FastScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }
    }

    static class Capture
    {
        public static Bitmap Desktop()
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        public static Rectangle Selection(Point first, Point last, Size size)
        {
            int left = Math.Max(0, Math.Min(size.Width, Math.Min(first.X, last.X)));
            int top = Math.Max(0, Math.Min(size.Height, Math.Min(first.Y, last.Y)));
            int right = Math.Max(0, Math.Min(size.Width, Math.Max(first.X, last.X)));
            int bottom = Math.Max(0, Math.Min(size.Height, Math.Max(first.Y, last.Y)));
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        public static string Save(Bitmap bitmap, string folder)
        {
            using (LocalFolderGuard guard = LocalFolderGuard.Acquire(folder, true))
            {
                string name = "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N") + ".png";
                using (FileStream stream = guard.CreateNewFile(name)) bitmap.Save(stream, ImageFormat.Png);
                return Path.Combine(guard.Folder, name);
            }
        }
    }

    sealed class SelectionWindow : Form
    {
        readonly Bitmap desktop;
        readonly SelectionCursor selectionCursor;
        Point origin;
        Point current;
        bool dragging;
        public Rectangle SelectedArea { get; private set; }

        public SelectionWindow(Bitmap bitmap)
        {
            desktop = bitmap;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            selectionCursor = new SelectionCursor();
            Cursor = selectionCursor.Cursor;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Cancel(); };
            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) { Cancel(); return; }
                if (e.Button != MouseButtons.Left) return;
                origin = current = e.Location; dragging = true; Capture = true; Invalidate();
            };
            MouseMove += delegate(object sender, MouseEventArgs e) { if (dragging) { current = e.Location; Invalidate(); } };
            MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (!dragging || e.Button != MouseButtons.Left) return;
                current = e.Location;
                SelectedArea = LocalScreenshot.Capture.Selection(origin, current, ClientSize);
                dragging = false; Capture = false;
                if (SelectedArea.Width > 0 && SelectedArea.Height > 0) { DialogResult = DialogResult.OK; Close(); }
                else Invalidate();
            };
        }

        void Cancel() { DialogResult = DialogResult.Cancel; Close(); }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && selectionCursor != null) selectionCursor.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(desktop, Point.Empty);
            using (Brush shade = new SolidBrush(Color.FromArgb(125, Color.Black))) e.Graphics.FillRectangle(shade, ClientRectangle);
            Rectangle area = LocalScreenshot.Capture.Selection(origin, current, ClientSize);
            if (dragging && area.Width > 0 && area.Height > 0)
            {
                e.Graphics.DrawImage(desktop, area, area, GraphicsUnit.Pixel);
                using (Pen border = new Pen(Color.FromArgb(100, 180, 255), 2)) e.Graphics.DrawRectangle(border, area.X, area.Y, area.Width - 1, area.Height - 1);
            }
            string text = dragging ? area.Width + " × " + area.Height + " px   ·   Esc — отмена" : "Выделите область мышью   ·   Esc или правая кнопка — отмена";
            Rectangle screen = Screen.FromPoint(Cursor.Position).Bounds;
            Point label = new Point(screen.Left - Left + 24, screen.Top - Top + 24);
            Size size = TextRenderer.MeasureText(text, Font);
            e.Graphics.FillRectangle(Brushes.Black, label.X - 10, label.Y - 8, size.Width + 20, size.Height + 16);
            TextRenderer.DrawText(e.Graphics, text, Font, label, Color.White);
        }
    }

}
