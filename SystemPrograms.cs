using System;
using System.Diagnostics;
using System.IO;

namespace LocalScreenshot
{
    static class SystemPrograms
    {
        internal static ProcessStartInfo ExplorerStartInfo(string folder)
        {
            // The Windows special folder is resolved by the OS, not PATH or WINDIR.
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (String.IsNullOrEmpty(windows) || !Path.IsPathRooted(windows))
                throw new IOException("Не удалось определить системную папку Windows.");
            return new ProcessStartInfo
            {
                FileName = Path.Combine(windows, "explorer.exe"),
                Arguments = "\"" + Path.GetFullPath(folder) + "\"",
                WorkingDirectory = windows,
                UseShellExecute = false
            };
        }
    }
}
