using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("FastScreen")]
[assembly: System.Reflection.AssemblyProduct("FastScreen")]

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
            if (args.Length == 2 && args[0] == "--render-test")
            {
                try { BehaviorTests.RenderInterface(args[1]); Environment.ExitCode = 0; }
                catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length == 2 && args[0] == "--logic-test")
            {
                try { BehaviorTests.Run(args[1]); Environment.ExitCode = 0; }
                catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); Environment.ExitCode = 1; }
                return;
            }
            if (args.Length == 2 && args[0] == "--self-test")
            {
                try { SelfTest.Run(args[1]); Environment.ExitCode = 0; }
                catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); Environment.ExitCode = 1; }
                return;
            }
            Application.Run(new MainWindow());
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
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".png");
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                bitmap.Save(stream, ImageFormat.Png);
            return path;
        }
    }

    sealed class SelectionWindow : Form
    {
        readonly Bitmap desktop;
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
            Cursor = Cursors.Cross;
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

    static class SelfTest
    {
        static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); }
        public static void Run(string report)
        {
            BehaviorTests.Run(report);
            BehaviorTests.CaptureLifecycle(report);
            Size bounds = new Size(300, 200);
            Check(Capture.Selection(new Point(250, 150), new Point(20, 30), bounds) == new Rectangle(20, 30, 230, 120), "reverse selection");
            Check(Capture.Selection(new Point(-30, -10), new Point(350, 210), bounds) == new Rectangle(0, 0, 300, 200), "selection clipping");
            Check(Capture.Selection(new Point(10, 10), new Point(10, 10), bounds).Size == Size.Empty, "empty selection");
            Check(MainWindow.IsNetworkPath(@"\\server\share"), "UNC rejected");
            Check(!MainWindow.IsNetworkPath(Path.GetFullPath(Path.GetDirectoryName(report))), "local folder accepted");
            string testFolder = Path.Combine(Path.GetDirectoryName(report), "capture-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testFolder);
            try
            {
                using (Bitmap desktop = Capture.Desktop())
                {
                    Check(desktop.Size == SystemInformation.VirtualScreen.Size, "full desktop size");
                    string first = Capture.Save(desktop, testFolder);
                    string second = Capture.Save(desktop, testFolder);
                    Check(first != second, "unique filenames");
                    using (Bitmap read = new Bitmap(first)) Check(read.Size == desktop.Size && read.RawFormat.Guid == ImageFormat.Png.Guid, "PNG round trip");
                    using (Bitmap crop = desktop.Clone(new Rectangle(0, 0, Math.Min(100, desktop.Width), Math.Min(80, desktop.Height)), PixelFormat.Format32bppArgb))
                    {
                        string cropped = Capture.Save(crop, testFolder);
                        using (Bitmap read = new Bitmap(cropped)) Check(read.Size == crop.Size && read.GetPixel(0, 0) == desktop.GetPixel(0, 0), "region PNG round trip");
                    }
                }
                File.AppendAllText(report, "PASS: selection in both directions; clipping; empty selection; local-only folder guard; full desktop capture; unique filenames; PNG encoding/decoding; region capture.\r\n");
            }
            finally { foreach (string path in Directory.GetFiles(testFolder)) File.Delete(path); Directory.Delete(testFolder); }
        }
    }
}
