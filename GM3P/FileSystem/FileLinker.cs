using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace GM3P.FileSystem
{
    public interface IFileLinker
    {
        void LinkOrCopy(string src, string dst);
    }

    public class FileLinker : IFileLinker
    {
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("libc", SetLastError = true)]
        static extern int link(string existingFile, string newFile);

        public void LinkOrCopy(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            // Best-effort delete: AV/Indexer may briefly hold the dest file
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (File.Exists(dst))
                        File.Delete(dst);
                    break;
                }
                catch
                {
                    Thread.Sleep(100 * (i + 1));
                }
            }

            // Hard-link only if same volume; otherwise copy
            bool sameVolume = string.Equals(
                Path.GetPathRoot(src),
                Path.GetPathRoot(dst),
                StringComparison.OrdinalIgnoreCase);

            if (sameVolume)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        if (!CreateHardLink(dst, src, IntPtr.Zero))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        return; // linked OK
                    }
                    else
                    {
                        if (link(src, dst) == 0)
                            return; // linked OK
                    }
                }
                catch
                {
                    // Fall through to copy
                }
            }

            // Fallback copy with retries
            for (int i = 0; i < 12; i++)
            {
                try
                {
                    File.Copy(src, dst, overwrite: true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(120 * (i + 1));
                }
            }

            // Last attempt (surface error if still failing)
            File.Copy(src, dst, overwrite: true);
        }
    }
}