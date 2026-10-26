using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocalScreenshot
{
    static class SettingsStorageTests
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); }
        public static void Run(string reportFolder)
        {
            string root = Path.Combine(reportFolder, "settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string folderFile = Path.Combine(root, "folder.txt"), hotkeys = Path.Combine(root, "hotkeys.txt");
            string fallback = Path.Combine(root, "Default"), valid = Path.Combine(root, "Снимки экрана");
            try
            {
                Check(FolderSettings.Read(folderFile, fallback) == fallback, "missing folder setting falls back");
                foreach (string invalid in new string[] { "", "  ", "C:\\bad\0folder", "C:\\bad\nfolder", "relative", "C:relative", @"\relative", @"\\server\share", @"C:\NUL", @"C:\bad*folder" })
                {
                    File.WriteAllText(folderFile, invalid);
                    Check(FolderSettings.Read(folderFile, fallback) == fallback, "invalid folder falls back");
                    Check(File.ReadAllText(folderFile) == invalid, "bad setting not silently overwritten");
                }
                File.WriteAllBytes(folderFile, new byte[] { 0xc3, 0x28 });
                Check(FolderSettings.Read(folderFile, fallback) == fallback, "malformed UTF-8 falls back");
                File.WriteAllText(folderFile, new string('x', SettingsStorage.FolderLimit + 1));
                Check(FolderSettings.Read(folderFile, fallback) == fallback, "oversized folder config falls back");
                File.WriteAllText(folderFile, valid, new UTF8Encoding(true));
                Check(FolderSettings.Read(folderFile, fallback) == valid, "UTF-8 BOM and Unicode folder supported");
                using (FileStream locked = new FileStream(folderFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Check(FolderSettings.Read(folderFile, fallback) == fallback, "unreadable config falls back");
                Check(FolderSettings.Save(folderFile, valid) == valid && FolderSettings.Read(folderFile, fallback) == valid, "folder atomic save and reload");

                Preferences defaults = new Preferences();
                foreach (string invalid in new string[] { "", "6:70", "6:70\n6:70", "6:70\n6:65\nextra", "6:70\ninvalid", "0:70\n6:65", "6:99999\n6:65", new string('x', SettingsStorage.HotkeyLimit + 1) })
                {
                    File.WriteAllText(hotkeys, invalid);
                    Preferences read = Preferences.Read(hotkeys);
                    Check(read.Full.Encode() == defaults.Full.Encode() && read.Region.Encode() == defaults.Region.Encode(), "invalid hotkeys restore both defaults");
                }
                File.WriteAllBytes(hotkeys, new byte[] { 0xff, 0xfe, 0xff });
                Check(Preferences.Read(hotkeys).Full.Encode() == defaults.Full.Encode(), "invalid hotkey encoding falls back");
                defaults.Save(hotkeys);
                string previous = File.ReadAllText(hotkeys);
                bool rejected = false;
                try { new Preferences { Full = defaults.Full, Region = defaults.Full }.Save(hotkeys); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected && File.ReadAllText(hotkeys) == previous, "invalid save preserves previous settings");

                string sentinel = Path.Combine(root, "folder.txt.tmp");
                File.WriteAllText(sentinel, "keep");
                FolderSettings.Save(folderFile, valid);
                Check(File.ReadAllText(sentinel) == "keep", "old fixed-name temp file untouched");
                string blocked = Path.Combine(root, "blocked"); Directory.CreateDirectory(blocked);
                rejected = false;
                try { SettingsStorage.WriteAtomic(blocked, "new", 100); }
                catch (IOException) { rejected = true; }
                Check(rejected && Directory.GetFiles(root, ".*.tmp").Length == 0, "failed commit cleans only its temporary file");
                Directory.Delete(blocked);

                string concurrent = Path.Combine(root, "concurrent.txt");
                string first = new string('A', 400), second = new string('B', 400), text;
                SettingsStorage.WriteAtomic(concurrent, first, 1024);
                using (ManualResetEventSlim start = new ManualResetEventSlim(false))
                {
                    int successful = 0;
                    Action<string> writer = delegate(string value)
                    {
                        start.Wait();
                        for (int i = 0; i < 12; i++)
                            try { SettingsStorage.WriteAtomic(concurrent, value, 1024); Interlocked.Increment(ref successful); }
                            catch (IOException) { } // Conflicting commits may fail, but must not corrupt the file.
                    };
                    Task a = Task.Factory.StartNew(delegate { writer(first); });
                    Task b = Task.Factory.StartNew(delegate { writer(second); });
                    start.Set();
                    for (int i = 0; i < 100; i++)
                        if (SettingsStorage.TryRead(concurrent, 1024, out text)) Check(text == first || text == second, "read sees complete config during concurrent saves");
                    Task.WaitAll(a, b);
                    Check(successful > 0 && SettingsStorage.TryRead(concurrent, 1024, out text) && (text == first || text == second), "concurrent commits leave complete config");
                }
                Check(Directory.GetFiles(root, ".*.tmp").Length == 0, "no orphan temporary settings files");
            }
            finally { foreach (string file in Directory.GetFiles(root)) File.Delete(file); foreach (string dir in Directory.GetDirectories(root)) Directory.Delete(dir); Directory.Delete(root); }
        }
    }
}
