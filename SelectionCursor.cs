using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalScreenshot
{
    sealed class SelectionCursor : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct IconInfo
        {
            [MarshalAs(UnmanagedType.Bool)] public bool IsIcon;
            public uint XHotspot, YHotspot;
            public IntPtr Mask, Color;
        }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", SetLastError = true)] static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);

        IntPtr handle;
        public Cursor Cursor { get; private set; }
        public SelectionCursor()
        {
            using (Bitmap image = CreateImage())
            {
                IntPtr icon = image.GetHicon();
                IconInfo info = new IconInfo();
                try
                {
                    if (!GetIconInfo(icon, out info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    info.IsIcon = false; info.XHotspot = info.YHotspot = 24;
                    handle = CreateIconIndirect(ref info);
                    if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    try { Cursor = new Cursor(handle); }
                    catch { DestroyCursor(handle); handle = IntPtr.Zero; throw; }
                }
                finally
                {
                    if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
                    if (info.Color != IntPtr.Zero) DeleteObject(info.Color);
                    DestroyIcon(icon);
                }
            }
        }
        internal static Bitmap CreateImage()
        {
            Bitmap image = new Bitmap(48, 48);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.Transparent); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen outline = new Pen(Color.White, 8))
                using (Pen blue = new Pen(Color.FromArgb(0, 120, 255), 4))
                {
                    outline.StartCap = outline.EndCap = blue.StartCap = blue.EndCap = LineCap.Round;
                    foreach (Pen pen in new Pen[] { outline, blue })
                    { graphics.DrawLine(pen, 5, 24, 43, 24); graphics.DrawLine(pen, 24, 5, 24, 43); }
                }
            }
            return image;
        }
        public void Dispose()
        {
            if (Cursor != null) { Cursor.Dispose(); Cursor = null; }
            if (handle != IntPtr.Zero) { DestroyCursor(handle); handle = IntPtr.Zero; }
        }
    }
}
