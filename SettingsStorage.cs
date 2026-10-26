using System;
using System.IO;
using System.Security;
using System.Text;

namespace LocalScreenshot
{
    static class SettingsStorage
    {
        internal const int FolderLimit = 4096, HotkeyLimit = 1024;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static bool TryRead(string file, int limit, out string text)
        {
            text = null;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length > limit) return false;
                    byte[] buffer = new byte[limit + 1];
                    int count = 0, read;
                    while (count < buffer.Length && (read = stream.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                    if (count > limit) return false;
                    int skip = count >= 3 && buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191 ? 3 : 0;
                    text = Utf8.GetString(buffer, skip, count - skip);
                    return true;
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { } catch (NotSupportedException) { } catch (SecurityException) { }
            return false;
        }

        internal static void WriteAtomic(string file, string text, int limit)
        {
            byte[] bytes = Utf8.GetBytes(text);
            if (bytes.Length > limit) throw new ArgumentException("Файл настроек превышает допустимый размер.");
            string destination = Path.GetFullPath(file), directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            bool created = false;
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (created)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }

    static class FolderSettings
    {
        internal static string Read(string file, string fallback)
        {
            string text, path;
            return SettingsStorage.TryRead(file, SettingsStorage.FolderLimit, out text) && TryNormalize(text, out path) ? path : fallback;
        }

        internal static bool TryNormalize(string value, out string path)
        {
            path = null;
            try
            {
                using (LocalFolderGuard guard = LocalFolderGuard.Acquire(value, false)) { path = guard.Folder; return true; }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { } catch (NotSupportedException) { } catch (SecurityException) { }
            return false;
        }

        internal static bool TryNormalizeSyntax(string value, out string path)
        {
            path = null;
            if (String.IsNullOrWhiteSpace(value)) return false;
            string candidate = value.Trim();
            // Require a full drive-qualified path; C:folder and \folder are relative.
            if (candidate.Length < 3 || !Char.IsLetter(candidate[0]) || candidate[1] != ':' ||
                (candidate[2] != '\\' && candidate[2] != '/')) return false;
            try
            {
                foreach (string part in candidate.Substring(3).Split(new char[] { '\\', '/' }))
                {
                    if (part == "" || part == "." || part == "..") continue;
                    if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith(".") || part.EndsWith(" ")) return false;
                    string device = part.Split('.')[0].ToUpperInvariant();
                    if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" ||
                        (device.Length == 4 && (device.StartsWith("COM") || device.StartsWith("LPT")) && device[3] >= '1' && device[3] <= '9')) return false;
                }
                string normalized = Path.GetFullPath(candidate);
                DriveType type = new DriveInfo(Path.GetPathRoot(normalized)).DriveType;
                if (type != DriveType.Fixed && type != DriveType.Removable && type != DriveType.Ram) return false;
                path = normalized;
                return true;
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { } catch (NotSupportedException) { } catch (SecurityException) { }
            return false;
        }

        internal static string Save(string file, string folder)
        {
            string normalized;
            if (!TryNormalize(folder, out normalized)) throw new ArgumentException("Выберите корректную папку на локальном диске.");
            SettingsStorage.WriteAtomic(file, normalized, SettingsStorage.FolderLimit);
            return normalized;
        }
    }
}
