using System;
using System.IO;
using System.Runtime.InteropServices;
using LocalScreenshot;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
static class FolderGuardTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main(string[] args)
    {
        try
        {
            string root = args[0], real = Path.Combine(root, "real"), alias = Path.Combine(root, "alias"), normalized;
            Check((File.GetAttributes(alias) & FileAttributes.ReparsePoint) != 0, "junction fixture exists");
            Check(FolderSettings.TryNormalize(real, out normalized), "ordinary local folder accepted");
            Check(!FolderSettings.TryNormalize(alias, out normalized), "junction rejected");
            Check(!FolderSettings.TryNormalize(Path.Combine(alias, "missing"), out normalized), "junction in parent rejected before traversal");
            Check(!Directory.Exists(Path.Combine(real, "missing")), "no directory created through rejected link");
            bool blocked = false;
            try { using (LocalFolderGuard guard = LocalFolderGuard.Acquire(alias, true)) { } }
            catch (IOException) { blocked = true; }
            Check(blocked, "save-time guard rejects junction");
            Check(!FolderSettings.TryNormalize(@"\\server\share", out normalized), "UNC rejected without accessing server");
            Check(!FolderSettings.TryNormalize(@"\\?\C:\folder", out normalized), "device namespace rejected");

            string destination = Path.Combine(real, "new", "nested");
            using (LocalFolderGuard guard = LocalFolderGuard.Acquire(destination, true))
            {
                using (FileStream stream = guard.CreateNewFile("test.bin")) { stream.WriteByte(17); stream.WriteByte(29); }
                Check(File.ReadAllBytes(Path.Combine(destination, "test.bin"))[0] == 17, "write to checked file handle succeeds");
                blocked = false;
                try { Directory.Move(real, Path.Combine(root, "moved")); }
                catch (IOException) { blocked = true; }
                Check(blocked, "checked ancestor cannot be renamed while saving");
                blocked = false;
                try { using (FileStream stream = guard.CreateNewFile("test.bin")) { } }
                catch (IOException) { blocked = true; }
                Check(blocked && new FileInfo(Path.Combine(destination, "test.bin")).Length == 2, "existing file never overwritten");
            }
            Directory.Move(real, Path.Combine(root, "moved"));
            Directory.Move(Path.Combine(root, "moved"), real);
            Console.WriteLine("PASS: local and new folders; junction and linked parents rejected; UNC/device paths rejected; native file write; ancestor lock; no overwrite; handle release.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
