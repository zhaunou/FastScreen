using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace LocalScreenshot
{
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
            string normalized;
            Check(!FolderSettings.TryNormalize(@"\\server\share", out normalized), "UNC rejected");
            Check(FolderSettings.TryNormalize(Path.GetFullPath(Path.GetDirectoryName(report)), out normalized), "local folder accepted");
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
