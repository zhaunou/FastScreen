using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    static class Design
    {
        public static readonly Color Navy = Color.FromArgb(30, 39, 52);
        public static readonly Color Muted = Color.FromArgb(91, 106, 125);
        public static readonly Color Accent = Color.FromArgb(0, 103, 192);
        public static readonly Color Surface = Color.FromArgb(238, 244, 251);
        public static void PaintBehind(Control control, Graphics graphics)
        {
            FluentPanel panel = control.Parent as FluentPanel;
            if (panel == null) { graphics.Clear(control.Parent == null ? Surface : control.Parent.BackColor); return; }
            GraphicsState state = graphics.Save();
            try { graphics.TranslateTransform(-control.Left, -control.Top); panel.PaintSurface(graphics); }
            finally { graphics.Restore(state); }
        }
        public static GraphicsPath Round(RectangleF rect, float radius)
        {
            float diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        public static Label Text(string text, int x, int y, int width, int height, float size, Color color, bool bold)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), BackColor = Color.Transparent };
        }
        public static Button Button(string text, int x, int y, int width, bool primary)
        {
            Button button = new FluentButton { Primary = primary, Text = text, Location = new Point(x, y), Size = new Size(width, 44), FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent, ForeColor = primary ? Color.White : Navy,
                Font = new Font("Segoe UI", 10, FontStyle.Regular), Cursor = Cursors.Hand };
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public static void WindowStyle(IntPtr window)
        {
            int round = 2, caption = Surface.R | (Surface.G << 8) | (Surface.B << 16), text = Navy.R | (Navy.G << 8) | (Navy.B << 16);
            DwmSetWindowAttribute(window, 33, ref round, sizeof(int));
            DwmSetWindowAttribute(window, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(window, 36, ref text, sizeof(int));
        }
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

    sealed class FluentMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(226, 237, 249); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(226, 237, 249); } }
        public override Color MenuBorder { get { return Color.FromArgb(205, 217, 232); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(247, 250, 254); } }
    }

    sealed class FluentButton : Button
    {
        public bool Primary;
        public bool Selected;
        bool hover, pressed;
        public FluentButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Opaque, true);
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The complete background is painted in OnPaint, including WM_PRINT.
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Design.PaintBehind(this, e.Graphics);
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (pressed ? Color.FromArgb(0, 79, 152) : hover ? Color.FromArgb(26, 119, 205) : Design.Accent) :
                Selected ? Color.FromArgb(220, 235, 250) : pressed ? Color.FromArgb(229, 237, 246) : hover ? Color.FromArgb(246, 249, 253) : Color.White;
            if (!Enabled) fill = Color.FromArgb(235, 239, 245);
            using (GraphicsPath path = Design.Round(new RectangleF(1, 1, Width - 3, Height - 3), 5))
            using (Brush brush = new SolidBrush(fill))
            using (Pen border = new Pen(Primary && Enabled ? Color.FromArgb(0, 88, 165) : Color.FromArgb(208, 219, 232)))
            { g.FillPath(brush, path); g.DrawPath(border, path); }
            Color text = !Enabled ? Color.FromArgb(146, 155, 168) : Primary ? Color.White : Design.Navy;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(8, 1, Width - 16, Height - 3), text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                using (GraphicsPath path = Design.Round(new RectangleF(4, 4, Width - 9, Height - 9), 3))
                using (Pen focus = new Pen(Primary ? Color.White : Design.Accent)) g.DrawPath(focus, path);
        }
    }

    sealed class FluentPanel : Panel
    {
        public bool Gradient;
        public FluentPanel() { DoubleBuffered = true; BackColor = Design.Surface; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintSurface(e.Graphics);
        }
        internal void PaintSurface(Graphics graphics)
        {
            Design.PaintBehind(this, graphics);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Design.Round(new RectangleF(1, 1, Width - 3, Height - 3), 12))
            using (Brush fill = Gradient ? (Brush)new LinearGradientBrush(ClientRectangle, Color.FromArgb(235, 246, 255), Color.FromArgb(224, 231, 250), 25f) : new SolidBrush(Color.FromArgb(252, 253, 255)))
            using (Pen border = new Pen(Color.FromArgb(210, 222, 237)))
            { graphics.FillPath(fill, path); graphics.DrawPath(border, path); }
        }
    }

    sealed class HeroArt : Control
    {
        public HeroArt() { DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Width / 420f; g.ScaleTransform(scale, scale);
            using (Brush shadow = new SolidBrush(Color.FromArgb(22, 65, 103, 160)))
            using (GraphicsPath path = Design.Round(new RectangleF(35, 52, 350, 250), 20)) g.FillPath(shadow, path);
            using (GraphicsPath frame = Design.Round(new RectangleF(25, 40, 350, 250), 18))
            using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(25, 40, 350, 250), Color.FromArgb(250, 253, 255), Color.FromArgb(214, 233, 253), 65f))
            using (Pen border = new Pen(Color.White, 2)) { g.FillPath(fill, frame); g.DrawPath(border, frame); }
            using (Brush dot = new SolidBrush(Color.FromArgb(172, 194, 220)))
                for (int i = 0; i < 3; i++) g.FillEllipse(dot, 42 + i * 13, 55, 5, 5);
            using (GraphicsPath screen = Design.Round(new RectangleF(40, 79, 320, 194), 10))
            {
                GraphicsState state = g.Save(); g.SetClip(screen);
                using (LinearGradientBrush blue = new LinearGradientBrush(new Rectangle(40, 79, 320, 194), Color.FromArgb(119, 214, 247), Color.FromArgb(37, 111, 222), 65f)) g.FillPath(blue, screen);
                for (int i = 0; i < 9; i++)
                {
                    using (GraphicsPath ribbon = new GraphicsPath())
                    {
                        ribbon.AddBezier(25, 300 + i * 4, 272, 307 - i * 7, 53, 105 - i * 5, 271, 106 + i * 4);
                        ribbon.AddBezier(271, 106 + i * 4, 430, 107 + i * 5, 270, 222 + i * 2, 393, 281 + i * 4);
                        ribbon.AddBezier(393, 281 + i * 4, 240, 256 + i * 2, 426, 130 + i * 4, 270, 124 + i * 4);
                        ribbon.AddBezier(270, 124 + i * 4, 94, 113 - i * 2, 266, 327 - i * 5, 25, 316 + i * 4);
                        ribbon.CloseFigure();
                        using (LinearGradientBrush fold = new LinearGradientBrush(new Rectangle(30, 70, 340, 220), Color.FromArgb(10, 177 - i * 9, 246 - i * 5), Color.FromArgb(7, 46 + i * 3, 150 + i * 5), 35f))
                        using (Pen edge = new Pen(Color.FromArgb(80, 149, 235, 255), 1.2f)) { g.FillPath(fold, ribbon); g.DrawPath(edge, ribbon); }
                    }
                }
                g.Restore(state);
            }
            using (Pen selection = new Pen(Color.White, 2))
            { selection.DashStyle = DashStyle.Dash; g.DrawRectangle(selection, 104, 117, 204, 110); }
            foreach (Point point in new Point[] { new Point(104, 117), new Point(308, 117), new Point(104, 227), new Point(308, 227) })
                using (GraphicsPath handle = Design.Round(new RectangleF(point.X - 4, point.Y - 4, 8, 8), 2)) g.FillPath(Brushes.White, handle);
            using (GraphicsPath badge = Design.Round(new RectangleF(173, 251, 229, 53), 10))
            using (Pen edge = new Pen(Color.FromArgb(200, 219, 240))) { g.FillPath(Brushes.White, badge); g.DrawPath(edge, badge); }
            using (Font font = new Font("Segoe UI", 11))
            using (Brush text = new SolidBrush(Design.Accent)) g.DrawString("Скриншот в один клик", font, text, 187, 267);
        }
    }

    sealed class ShortcutBox : UserControl
    {
        sealed class Input : TextBox
        {
            public ShortcutBox Owner;
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab)) return base.ProcessCmdKey(ref msg, keyData);
                Shortcut candidate = Shortcut.FromKeys(keyData);
                if (candidate.Valid) Owner.SetShortcut(candidate);
                return true;
            }
        }
        readonly Input input;
        public Shortcut Shortcut { get; private set; }
        public ShortcutBox(Shortcut value)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            BackColor = Design.Surface;
            input = new Input { Owner = this, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White,
                ForeColor = Design.Navy, Font = new Font("Segoe UI", 12), AutoSize = false, AccessibleName = "Горячая клавиша" };
            input.GotFocus += delegate { Invalidate(); }; input.LostFocus += delegate { Invalidate(); };
            Controls.Add(input); SetShortcut(value);
        }
        public void SetShortcut(Shortcut value) { Shortcut = value; input.Text = value.ToString(); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (input == null) return;
            int textHeight = input.Font.Height + 3;
            input.SetBounds(14, Math.Max(4, (Height - textHeight) / 2), Math.Max(1, Width - 28), textHeight);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            Design.PaintBehind(this, e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Design.Round(new RectangleF(1, 1, Width - 3, Height - 3), 6))
            using (Pen border = new Pen(Color.FromArgb(195, 208, 224))) { e.Graphics.FillPath(Brushes.White, path); e.Graphics.DrawPath(border, path); }
            using (Pen line = new Pen(ContainsFocus ? Design.Accent : Color.FromArgb(139, 153, 173), ContainsFocus ? 2 : 1))
                e.Graphics.DrawLine(line, 7, Height - 3, Width - 8, Height - 3);
        }
    }
}
