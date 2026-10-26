using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LocalScreenshot
{
    // Reject all reparse points, including local links and cloud placeholders.
    // Retain directory handles without delete/write sharing until the write ends.
    sealed class LocalFolderGuard : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct AttributeTag { public uint Attributes, Tag; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateDirectoryW(string path, IntPtr security);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int kind, out AttributeTag info, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint capacity, uint flags);
        const uint OpenReparse = 0x00200000, BackupSemantics = 0x02000000;
        readonly List<SafeFileHandle> handles = new List<SafeFileHandle>();
        internal string Folder { get; private set; }
        LocalFolderGuard(string folder) { Folder = folder; }

        static IOException Error(string message) { return new IOException(message, new Win32Exception(Marshal.GetLastWin32Error())); }
        static void ValidateHandle(SafeFileHandle handle, bool directory)
        {
            AttributeTag info;
            if (!GetFileInformationByHandleEx(handle, 9, out info, 8)) throw Error("Не удалось проверить папку сохранения.");
            if ((info.Attributes & 0x400) != 0) throw new IOException("Папки со ссылками, перенаправлениями и облачными placeholder-файлами не поддерживаются. Выберите обычную локальную папку.");
            if (((info.Attributes & 0x10) != 0) != directory) throw new IOException("Некорректный тип объекта в пути сохранения.");
            StringBuilder final = new StringBuilder(32768);
            uint length = GetFinalPathNameByHandleW(handle, final, (uint)final.Capacity, 0);
            if (length == 0 || length >= final.Capacity) throw Error("Не удалось проверить конечное расположение файла.");
            string path = final.ToString();
            if (path.Length < 7 || !path.StartsWith(@"\\?\", StringComparison.Ordinal) || !Char.IsLetter(path[4]) || path[5] != ':' || path[6] != '\\')
                throw new IOException("Сохранение разрешено только на локальный диск.");
            DriveType type = new DriveInfo(path.Substring(4, 3)).DriveType;
            if (type != DriveType.Fixed && type != DriveType.Removable && type != DriveType.Ram)
                throw new IOException("Сохранение разрешено только на локальный диск.");
        }

        internal static LocalFolderGuard Acquire(string folder, bool create)
        {
            string normalized;
            if (!FolderSettings.TryNormalizeSyntax(folder, out normalized)) throw new IOException("Выберите корректную папку на локальном диске.");
            LocalFolderGuard guard = new LocalFolderGuard(normalized);
            try
            {
                string root = Path.GetPathRoot(normalized), current = root;
                string[] parts = normalized.Substring(root.Length).Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = -1; i < parts.Length; i++)
                {
                    if (i >= 0) current = Path.Combine(current, parts[i]);
                    // Deny write/delete sharing: inspected parents cannot be renamed
                    // or converted to reparse points while descendants are opened.
                    SafeFileHandle handle = CreateFileW(current, 0, 1, IntPtr.Zero, 3, OpenReparse | BackupSemantics, IntPtr.Zero);
                    if (handle.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error(); handle.Dispose();
                        if (error != 2 && error != 3) throw new IOException("Не удалось безопасно открыть папку сохранения.", new Win32Exception(error));
                        if (!create) break;
                        if (!CreateDirectoryW(current, IntPtr.Zero) && Marshal.GetLastWin32Error() != 183) throw Error("Не удалось создать папку сохранения.");
                        handle = CreateFileW(current, 0, 1, IntPtr.Zero, 3, OpenReparse | BackupSemantics, IntPtr.Zero);
                        if (handle.IsInvalid) { handle.Dispose(); throw Error("Не удалось проверить созданную папку."); }
                    }
                    guard.handles.Add(handle);
                    ValidateHandle(handle, true);
                }
                return guard;
            }
            catch { guard.Dispose(); throw; }
        }

        internal FileStream CreateNewFile(string name)
        {
            if (handles.Count == 0) throw new ObjectDisposedException("LocalFolderGuard");
            if (String.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Некорректное имя файла.");
            SafeFileHandle file = CreateFileW(Path.Combine(Folder, name), 0x40000000, 0, IntPtr.Zero, 1, OpenReparse | 0x80, IntPtr.Zero);
            if (file.IsInvalid) { file.Dispose(); throw Error("Не удалось создать файл скриншота."); }
            try { ValidateHandle(file, false); return new FileStream(file, FileAccess.Write); }
            catch { file.Dispose(); throw; }
        }

        public void Dispose()
        {
            for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
            handles.Clear();
        }
    }
}
