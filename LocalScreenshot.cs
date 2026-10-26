using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

    sealed class MainWindow : Form
    {
        readonly Label folderLabel = new Label();
        readonly Label statusLabel = new Label();
        readonly Button openLast = new Button();
        readonly Timer captureTimer = new Timer();
        readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalScreenshot", "folder.txt");
        string folder;
        string lastFile;
        bool regionMode;

        public MainWindow()
        {
            Text = "Локальный скриншот";
            ClientSize = new Size(560, 365);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 251);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            Label title = new Label { Text = "Снимок экрана", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) };
            Label subtitle = new Label { Text = "Только на вашем компьютере. Без загрузки в интернет.", AutoSize = true, ForeColor = Color.FromArgb(85, 95, 115), Location = new Point(27, 72) };
            Button full = MakeButton("Весь экран", 26, 114, 248, 58);
            full.BackColor = Color.FromArgb(37, 99, 235);
            full.ForeColor = Color.White;
            Button region = MakeButton("Выделить область", 286, 114, 248, 58);
            full.Click += delegate { BeginCapture(false); };
            region.Click += delegate { BeginCapture(true); };
            Label hint = new Label { Text = "Ctrl+Shift+F — весь экран  ·  Ctrl+Shift+A — область", AutoSize = true, Location = new Point(27, 184), ForeColor = Color.DimGray };

            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalScreenshot", "Screenshots");
            try { if (File.Exists(settingsFile)) { string saved = File.ReadAllText(settingsFile).Trim(); if (Path.IsPathRooted(saved) && !IsNetworkPath(saved)) folder = saved; } } catch { }
            folderLabel.SetBounds(27, 221, 390, 43);
            folderLabel.AutoEllipsis = true;
            UpdateFolderLabel();
            Button choose = MakeButton("Изменить…", 427, 222, 107, 34);
            choose.Click += ChooseFolder;
            statusLabel.SetBounds(27, 278, 505, 38);
            statusLabel.Text = "Готово к съёмке. Формат: PNG.";
            statusLabel.AutoEllipsis = true;
            openLast.SetBounds(26, 321, 248, 30);
            openLast.Text = "Открыть последний снимок";
            openLast.Enabled = false;
            openLast.Click += delegate { try { System.Diagnostics.Process.Start(lastFile); } catch (Exception ex) { ShowError(ex); } };
            Button openFolder = MakeButton("Открыть папку", 286, 321, 248, 30);
            openFolder.Click += delegate { try { EnsureLocalFolder(); Directory.CreateDirectory(folder); System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\""); } catch (Exception ex) { ShowError(ex); } };
            Controls.AddRange(new Control[] { title, subtitle, full, region, hint, folderLabel, choose, statusLabel, openLast, openFolder });
            captureTimer.Interval = 250;
            captureTimer.Tick += CaptureNow;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.Shift && (e.KeyCode == Keys.F || e.KeyCode == Keys.A))
                { e.SuppressKeyPress = true; BeginCapture(e.KeyCode == Keys.A); }
            };
            FormClosed += delegate { captureTimer.Dispose(); };
        }

        static Button MakeButton(string text, int x, int y, int width, int height)
        {
            return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
        }

        internal static bool IsNetworkPath(string path)
        {
            if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).DriveType == DriveType.Network; }
            catch { return true; }
        }

        void EnsureLocalFolder()
        {
            if (IsNetworkPath(folder)) throw new IOException("Выберите папку на локальном диске. Сетевые папки не поддерживаются.");
        }

        void UpdateFolderLabel() { folderLabel.Text = "Папка сохранения:\n" + folder; }

        void ChooseFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Папка для скриншотов на локальном диске", SelectedPath = folder })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (IsNetworkPath(dialog.SelectedPath)) { MessageBox.Show(this, "Выберите папку на локальном диске.", Text); return; }
                folder = dialog.SelectedPath;
                UpdateFolderLabel();
                try { Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)); File.WriteAllText(settingsFile, folder); }
                catch { statusLabel.Text = "Папка выбрана. Не удалось запомнить её для следующего запуска."; }
            }
        }

        void BeginCapture(bool region)
        {
            if (captureTimer.Enabled || !Visible) return;
            try { EnsureLocalFolder(); } catch (Exception ex) { ShowError(ex); return; }
            regionMode = region;
            Hide();
            captureTimer.Start();
        }

        void CaptureNow(object sender, EventArgs e)
        {
            captureTimer.Stop();
            try
            {
                using (Bitmap desktop = LocalScreenshot.Capture.Desktop())
                {
                    if (regionMode)
                    {
                        using (SelectionWindow selection = new SelectionWindow(desktop))
                        {
                            if (selection.ShowDialog() != DialogResult.OK) { statusLabel.Text = "Съёмка отменена."; return; }
                            using (Bitmap crop = desktop.Clone(selection.SelectedArea, PixelFormat.Format32bppArgb)) SaveShot(crop);
                        }
                    }
                    else SaveShot(desktop);
                }
            }
            catch (Exception ex) { ShowError(ex); }
            finally { Show(); Activate(); }
        }

        void SaveShot(Bitmap bitmap)
        {
            EnsureLocalFolder();
            lastFile = LocalScreenshot.Capture.Save(bitmap, folder);
            openLast.Enabled = true;
            statusLabel.Text = "Сохранено: " + Path.GetFileName(lastFile);
        }

        void ShowError(Exception ex)
        {
            Show();
            MessageBox.Show(this, "Не удалось выполнить действие.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                File.WriteAllText(report, "PASS: selection in both directions; clipping; empty selection; local-only folder guard; full desktop capture; unique filenames; PNG encoding/decoding; region capture.\r\n");
            }
            finally { foreach (string path in Directory.GetFiles(testFolder)) File.Delete(path); Directory.Delete(testFolder); }
        }
    }
}
