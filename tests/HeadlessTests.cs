using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace LocalScreenshot
{
    static class HeadlessTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); }
        internal static void Run(string report)
        {
            string root = Path.GetDirectoryName(report);
            SettingsStorageTests.Run(root);
            BehaviorTests.CheckSystemProgramLoading();
            BehaviorTests.CheckScreenshotSound();
            BehaviorTests.CheckSelectionCursor(root);
            Check(Capture.Selection(new Point(80, 60), new Point(10, 20), new Size(100, 100)) == new Rectangle(10, 20, 70, 40), "reverse selection");
            Check(Capture.Selection(new Point(-10, -20), new Point(200, 200), new Size(100, 100)) == new Rectangle(0, 0, 100, 100), "selection clipping");
            string folder = Path.Combine(root, "synthetic-png-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                using (Bitmap image = new Bitmap(80, 60))
                {
                    using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.CornflowerBlue);
                    image.SetPixel(10, 12, Color.OrangeRed);
                    string first = Capture.Save(image, folder), second = Capture.Save(image, folder);
                    Check(first != second, "unique screenshot filenames");
                    using (Bitmap loaded = new Bitmap(first))
                        Check(loaded.RawFormat.Guid == ImageFormat.Png.Guid && loaded.Size == image.Size && loaded.GetPixel(10, 12).ToArgb() == Color.OrangeRed.ToArgb(), "synthetic PNG round trip");
                    using (Bitmap crop = image.Clone(new Rectangle(5, 5, 30, 25), PixelFormat.Format32bppArgb))
                    {
                        string path = Capture.Save(crop, folder);
                        using (Bitmap loaded = new Bitmap(path)) Check(loaded.Size == crop.Size && loaded.GetPixel(5, 7).ToArgb() == Color.OrangeRed.ToArgb(), "synthetic region PNG round trip");
                    }
                }
            }
            finally { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
            File.WriteAllText(report, "PASS: bounded settings and atomic writes; system DLL policy; trusted Explorer path; embedded sound; native blue cursor; selection math; synthetic full/region PNG and unique filenames. No desktop capture or interactive windows.\r\n");
        }
    }
}
