using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    static class Design
    {
        public static readonly Color Navy = Color.FromArgb(19, 30, 52);
        public static readonly Color Muted = Color.FromArgb(109, 122, 144);
        public static readonly Color Accent = Color.FromArgb(84, 109, 246);
        public static Label Text(string text, int x, int y, int width, int height, float size, Color color, bool bold)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), BackColor = Color.Transparent };
        }
        public static Button Button(string text, int x, int y, int width, bool primary)
        {
            Button button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 44), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Navy,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(222, 228, 239);
            return button;
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        public static Icon CreateIcon()
        {
            using (Bitmap image = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(image))
            using (Pen pen = new Pen(Color.White, 3))
            {
                g.Clear(Accent); g.DrawRectangle(pen, 7, 8, 18, 15); g.FillEllipse(Brushes.White, 13, 12, 7, 7);
                IntPtr handle = image.GetHicon();
                try { using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }

    sealed class HeroArt : Control
    {
        public HeroArt() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Width / 420f; g.ScaleTransform(scale, scale);
            using (LinearGradientBrush glow = new LinearGradientBrush(new Rectangle(0, 0, 420, 350), Color.FromArgb(36, 53, 92), Color.FromArgb(19, 30, 52), 40f))
                g.FillEllipse(glow, 5, 10, 410, 330);
            using (Pen grid = new Pen(Color.FromArgb(38, 53, 80)))
                for (int x = 35; x < 420; x += 30) for (int y = 35; y < 330; y += 30) g.DrawEllipse(grid, x, y, 1, 1);
            using (Brush shadow = new SolidBrush(Color.FromArgb(12, 20, 37))) g.FillRectangle(shadow, 48, 68, 330, 217);
            using (Brush frame = new SolidBrush(Color.FromArgb(58, 75, 107))) g.FillRectangle(frame, 34, 48, 330, 217);
            using (LinearGradientBrush screen = new LinearGradientBrush(new Rectangle(40, 54, 318, 205), Color.FromArgb(57, 72, 128), Color.FromArgb(30, 43, 72), 90f)) g.FillRectangle(screen, 40, 54, 318, 205);
            using (Brush hill = new SolidBrush(Color.FromArgb(92, 112, 193))) g.FillPolygon(hill, new Point[] { new Point(40, 225), new Point(146, 119), new Point(260, 259), new Point(40, 259) });
            using (Brush hill = new SolidBrush(Color.FromArgb(64, 85, 156))) g.FillPolygon(hill, new Point[] { new Point(160, 259), new Point(269, 146), new Point(358, 236), new Point(358, 259) });
            using (Brush sun = new SolidBrush(Color.FromArgb(164, 190, 255))) g.FillEllipse(sun, 273, 78, 30, 30);
            using (Pen pen = new Pen(Color.FromArgb(130, 237, 218), 2))
            {
                pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, 103, 104, 207, 119);
                foreach (Point corner in new Point[] { new Point(103, 104), new Point(310, 104), new Point(103, 223), new Point(310, 223) })
                    g.FillRectangle(Brushes.White, corner.X - 4, corner.Y - 4, 8, 8);
            }
            using (Brush badge = new SolidBrush(Color.FromArgb(130, 237, 218))) g.FillRectangle(badge, 240, 241, 153, 42);
            using (Font font = new Font("Segoe UI", 11, FontStyle.Bold)) g.DrawString("PNG  /  ЛОКАЛЬНО", font, Brushes.MidnightBlue, 251, 252);
            using (Brush stand = new SolidBrush(Color.FromArgb(58, 75, 107))) { g.FillRectangle(stand, 182, 265, 34, 25); g.FillRectangle(stand, 142, 290, 114, 5); }
        }
    }

    sealed class ShortcutBox : TextBox
    {
        public Shortcut Shortcut { get; private set; }
        public ShortcutBox(Shortcut value)
        {
            ReadOnly = true; BackColor = Color.White; BorderStyle = BorderStyle.FixedSingle;
            Font = new Font("Segoe UI", 13); SetShortcut(value);
        }
        public void SetShortcut(Shortcut value) { Shortcut = value; Text = value.ToString(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab)) return base.ProcessCmdKey(ref msg, keyData);
            Shortcut candidate = Shortcut.FromKeys(keyData);
            if (candidate.Valid) SetShortcut(candidate);
            return true;
        }
    }
}
