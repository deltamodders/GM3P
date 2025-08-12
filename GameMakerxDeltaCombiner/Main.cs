using Codeuctivity.ImageSharpCompare;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Advanced;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GM3P
{
    public class ModFileInfo
    {
        public int ModNumber { get; set; }
        public string FilePath { get; set; }
        public string ModName { get; set; }
    }


    //Test: This class provides methods to create hard links or copy files.
    // It uses P/Invoke to call the CreateHardLink function on Windows and link function
    internal static class FileLinker
    {
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("libc", SetLastError = true)]
        static extern int link(string existingFile, string newFile);

        public static void LinkOrCopy(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            // best-effort delete: AV/Indexer may briefly hold the dest file
            for (int i = 0; i < 8; i++)
            {
                try { if (File.Exists(dst)) File.Delete(dst); break; }
                catch { Thread.Sleep(100 * (i + 1)); }
            }

            // hard-link only if same volume; otherwise copy
            bool sameVolume = string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dst), StringComparison.OrdinalIgnoreCase);

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
                        if (link(src, dst) == 0) return; // linked OK
                    }
                }
                catch
                {
                    // fall through to copy
                }
            }

            // fallback copy with retries
            for (int i = 0; i < 12; i++)
            {
                try { File.Copy(src, dst, overwrite: true); return; }
                catch (IOException) { Thread.Sleep(120 * (i + 1)); }
            }
            File.Copy(src, dst, overwrite: true); // last attempt (surface error if still failing)
        }
    }

    // Test: This class caches SHA1 hashes of files to avoid recomputing them
    // It uses a ConcurrentDictionary to store file paths as keys and their hashes as values.
    internal static class HashCache
    {
        private sealed class Entry
        {
            public long Len;
            public DateTime LwtUtc;
            public string Base64 = "";
        }

        private static readonly ConcurrentDictionary<string, Entry> _map = new();

        public static string Sha1Base64(string path)
        {
            string full = Path.GetFullPath(path);
            var fi = new FileInfo(full);

            var e = _map.GetOrAdd(full, _ => new Entry());
            if (e.Len == fi.Length && e.LwtUtc == fi.LastWriteTimeUtc && e.Base64.Length > 0)
                return e.Base64;

            using var fs  = File.OpenRead(full);
            using var sha = SHA1.Create();
            e.Base64 = Convert.ToBase64String(sha.ComputeHash(fs));
            e.Len    = fi.Length;
            e.LwtUtc = fi.LastWriteTimeUtc;
            return e.Base64;
        }
    }

    internal class Main
    {
        /// <summary>
        /// The path to the vanilla game
        /// </Summary>
        public static string? vanilla2 { get; set; }
        /// <summary>
        /// Current working directory
        /// </summary>
        public static string? pwd = Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetExecutingAssembly().Location)));
        /// <summary>
        /// Output folder
        /// </summary>
        public static string? output { get; set; }
        /// <summary>
        /// path to an xDelta patcher, e.g. xDelta3 or Deltapatcher
        /// </summary>
        public static string? DeltaPatcher { get; set; }
        /// <summary>
        /// Amount of mods to merge
        /// </summary>
        public static int modAmount { get; set; }
        /// <summary>
        /// Currently unused except as a CLI arg, but this will be used to determine what Game Engine the game is in in a far future release. Use "GM" is for GameMaker
        /// </summary>
        public static string? gameEngine { get; set; }
        /// <summary>
        /// A bool to tell if compareCombine() has been called
        /// </summary>
        public static bool combined { get; set; }
        /// <summary>
        /// How many chapters vanilla has
        /// </summary>
        public static int chapterAmount { get; set; }
        /// <summary>
        /// Path to the modTool for Dumping
        /// </summary>
        public static string? modTool { get; set; }
        public static string[] xDeltaFile { get; set; }

        private static readonly byte[] PNG_SIGNATURE = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static List<string> modifiedAssets = new List<string> { "Asset Name                       Hash (SHA1 in Base64)" };

        // <summary>
        /// A cache for mod numbers to avoid errors in writing to modnumberscache.txt
        /// </summary>
        private static readonly object ModNumbersCacheLock = new object();


        private static string DumpCacheDirByHash(int chapter, string sig)
        {
            var shard = string.IsNullOrEmpty(sig) ? "__" : sig.Substring(0, Math.Min(2, sig.Length));
            return Path.Combine(@output, "Cache", "export", chapter.ToString(), "byhash", shard, sig);
        }
        private static string DumpStampPathByHash(int chapter, string sig)
            => Path.Combine(DumpCacheDirByHash(chapter, sig), ".stamp");

        static string DumpCacheDir(int chapter, int modNumber)
            => Path.Combine(@output, "Cache", "exports", chapter.ToString(), modNumber.ToString());

        static string DumpStampPath(int chapter, int modNumber)
            => Path.Combine(DumpCacheDir(chapter, modNumber), "dump.sha1");


        // --- Cache toggles (env) ---
        static bool CacheEnabled()
        {
            var v = Environment.GetEnvironmentVariable("GM3P_EXPORT_CACHE");
            // default ON; "0" disables
            return !string.Equals(v, "0", StringComparison.Ordinal);
        }

        static bool CacheSpritesEnabled()
        {
            var v = Environment.GetEnvironmentVariable("GM3P_EXPORT_CACHE_SPRITES");
            // default ON; "0" disables sprite caching
            return !string.Equals(v, "0", StringComparison.Ordinal);
        }


        /// <summary>
        /// Returns a line from a text file as a string
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static string GetLine(string fileName, int line)
        {
            using (var sr = new StreamReader(fileName))
            {
                for (int i = 1; i < line; i++)
                    sr.ReadLine();
                return sr.ReadLine();
            }
        }
        /// <summary>
        /// TEST: this should help with optimization for xDelta patching.
        /// </summary>
        static int XdeltaConcurrency =>
            Math.Max(1, Math.Min(Environment.ProcessorCount,
                int.TryParse(Environment.GetEnvironmentVariable("GM3P_XDELTA_CONCURRENCY"), out var n) ? n : 3));

        /// <summary>
        /// Validate if a file is a valid PNG
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool IsValidPNG(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 8)
                    return false;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[8];
                    fs.Read(header, 0, 8);
                    return header.SequenceEqual(PNG_SIGNATURE);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Find the best sprite version from available mods
        /// </summary>
        /// <param name="sprites"></param>
        /// <param name="vanillaVersion"></param>
        /// <returns></returns>
        private static ModFileInfo SelectBestSprite(List<ModFileInfo> sprites, ModFileInfo vanillaVersion = null)
        {
            Console.WriteLine($"    Selecting best sprite from {sprites.Count} version(s)");

            // First, try to find a valid PNG from the mods (prioritize later mods)
            for (int i = sprites.Count - 1; i >= 0; i--)
            {
                if (IsValidPNG(sprites[i].FilePath))
                {
                    var info = new FileInfo(sprites[i].FilePath);
                    Console.WriteLine($"      Selected valid PNG from {sprites[i].ModName} ({info.Length} bytes)");
                    return sprites[i];
                }
                else
                {
                    Console.WriteLine($"      WARNING: Invalid PNG from {sprites[i].ModName}");
                }
            }

            // If no valid mod sprites, fall back to vanilla
            if (vanillaVersion != null && IsValidPNG(vanillaVersion.FilePath))
            {
                Console.WriteLine($"      All mod sprites invalid, using vanilla");
                return vanillaVersion;
            }

            // Last resort: return the first sprite even if invalid
            Console.WriteLine($"      WARNING: No valid sprites found, using {sprites[0].ModName} anyway");
            return sprites[0];
        }

        /// <summary>
        /// Validate all sprites after merge
        /// </summary>
        /// <param name="chapter"></param>
        private static void ValidateSprites(int chapter)
        {
            string spritesPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", "Sprites");
            if (!Directory.Exists(spritesPath))
                return;

            int total = 0;
            var invalidSprites = new List<string>();

            foreach (var pngFile in Directory.EnumerateFiles(spritesPath, "*.png", SearchOption.AllDirectories))
            {
                total++;
                if (!IsValidPNG(pngFile))
                    invalidSprites.Add(Path.GetFileName(pngFile));
            }

            if (invalidSprites.Count > 0)
            {
                Console.WriteLine($"  WARNING: {invalidSprites.Count} invalid sprites detected after merge:");
                foreach (var sprite in invalidSprites.Take(10))
                    Console.WriteLine($"    - {sprite}");
                if (invalidSprites.Count > 10)
                    Console.WriteLine($"    ... and {invalidSprites.Count - 10} more");
            }
            else
            {
                Console.WriteLine($"  ✓ All {total} sprites validated successfully");
            }
        }


        /// <summary>
        /// Compare sprites considering they might be part of animation strips
        /// </summary>
        /// <param name="sprite1Path"></param>
        /// <param name="sprite2Path"></param>
        /// <returns></returns>
        private static bool AreSpritesDifferent(string modPng, string vanillaPng)
        {
            try
            {
                // If mod PNG is invalid, skip importing it (avoid breaking import)
                if (!IsValidPNG(modPng)) return false;

                // 1) Fast path: exact file hash match -> identical
                try
                {
                    var h1 = HashCache.Sha1Base64(modPng);
                    var h2 = HashCache.Sha1Base64(vanillaPng);
                    if (h1 == h2) return false;
                }
                catch { /* fall through */ }

                // 2) Geometry check (IHDR)
                var s1 = TryGetPngSize(modPng);
                var s2 = TryGetPngSize(vanillaPng);
                if (s1.HasValue && s2.HasValue &&
                    (s1.Value.width != s2.Value.width || s1.Value.height != s2.Value.height))
                    return true;

                // 3) Pixel compare when available
                try
                {
                    return !Codeuctivity.ImageSharpCompare.ImageSharpCompare.ImagesAreEqual(modPng, vanillaPng);
                }
                catch
                {
                    // 4) Fallback: compare only IDAT data (ignores metadata/gAMA/iCCP, etc.)
                    var idat1 = HashPngIdat(modPng);
                    var idat2 = HashPngIdat(vanillaPng);
                    if (idat1 != null && idat2 != null)
                        return !idat1.SequenceEqual(idat2);

                    // 5) Last resort: different file hashes already seen above; if we got here,
                    // we couldn't compute IDAT hashes; assume different so sprite changes apply.
                    try
                    {
                        using var a = File.OpenRead(modPng);
                        using var b = File.OpenRead(vanillaPng);
                        return !SHA1.Create().ComputeHash(a).SequenceEqual(SHA1.Create().ComputeHash(b));
                    }
                    catch { return true; }
                }
            }
            catch
            {
                // Conservative but in favor of applying mods when something changed
                return true;
            }
        }

        // Hash concatenated IDAT chunk payloads (ignores all ancillary chunks)
        private static byte[]? HashPngIdat(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                // PNG signature
                byte[] sig = br.ReadBytes(8);
                if (sig.Length != 8 || sig[0] != 0x89) return null;

                using var sha1 = SHA1.Create();
                bool sawIdat = false;

                while (fs.Position < fs.Length)
                {
                    // chunk length (big endian)
                    var lenBytes = br.ReadBytes(4);
                    if (lenBytes.Length < 4) break;
                    int len = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];

                    // chunk type
                    var type = br.ReadBytes(4);
                    if (type.Length < 4) break;

                    // data
                    var data = br.ReadBytes(len);
                    if (data.Length < len) break;

                    // crc
                    br.ReadUInt32(); // skip CRC

                    // IDAT?
                    if (type[0] == (byte)'I' && type[1] == (byte)'D' && type[2] == (byte)'A' && type[3] == (byte)'T')
                    {
                        sawIdat = true;
                        sha1.TransformBlock(data, 0, data.Length, null, 0);
                    }

                    // IEND ends the stream
                    if (type[0] == (byte)'I' && type[1] == (byte)'E' && type[2] == (byte)'N' && type[3] == (byte)'D')
                        break;
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return sawIdat ? sha1.Hash : null;
            }
            catch { return null; }
        }


        private static string FindGit()
        {
            try
            {
                // Check for portable Git in GM3P folder FIRST
                string[] portableGitPaths = {
                    Path.Combine(pwd, "git", "cmd", "git.exe"),
                    Path.Combine(pwd, "git", "bin", "git.exe"),
                    Path.Combine(pwd, "git", "mingw64", "bin", "git.exe")
                };

                foreach (string portableGit in portableGitPaths)
                {
                    if (File.Exists(portableGit))
                    {
                        Console.WriteLine($"  Found Git at: {portableGit}");

                        // Test if git actually works
                        try
                        {
                            using (var testProcess = new Process())
                            {
                                testProcess.StartInfo.FileName = portableGit;
                                testProcess.StartInfo.Arguments = "--version";
                                testProcess.StartInfo.CreateNoWindow = true;
                                testProcess.StartInfo.UseShellExecute = false;
                                testProcess.StartInfo.RedirectStandardOutput = true;
                                testProcess.StartInfo.RedirectStandardError = true;
                                testProcess.Start();

                                string version = testProcess.StandardOutput.ReadToEnd();
                                testProcess.WaitForExit(2000);

                                if (testProcess.ExitCode == 0 && version.Contains("git version"))
                                {
                                    Console.WriteLine($"  Git version: {version.Trim()}");
                                    return portableGit;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Git test failed: {ex.Message}");
                        }
                    }
                }

                // Try system git
                using (var process = new Process())
                {
                    process.StartInfo.FileName = OperatingSystem.IsWindows() ? "where" : "which";
                    process.StartInfo.Arguments = "git";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();

                    string output = process.StandardOutput.ReadLine();
                    process.WaitForExit(2000);

                    if (!string.IsNullOrEmpty(output) && File.Exists(output.Trim()))
                    {
                        Console.WriteLine($"  Found system Git at: {output.Trim()}");
                        return output.Trim();
                    }
                }

                Console.WriteLine("  WARNING: Git not found!");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error finding Git: {ex.Message}");
                return null;
            }
        }

        public static void loadCachedNumbers()
        {
            if (File.Exists(@output + "/Cache/running/chapterAmount.txt"))
            {
                chapterAmount = Convert.ToInt32(File.ReadAllText(@output + "/Cache/running/chapterAmount.txt"));
            }
            else
            {
                chapterAmount = 1;
            }
        }
        /// <summary>
        /// Creates the folders where other functions in this class works in
        /// </summary>
        public static void CreateCombinerDirectories()
        {
            Directory.CreateDirectory(@output + "/Cache/vanilla");
            Directory.CreateDirectory(@output + "/Cache/running");
            Directory.CreateDirectory(@output + "/xDeltaCombiner");

            if (File.Exists(@output + "/Cache/running/chapterAmount.txt"))
                loadCachedNumbers();

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    Directory.CreateDirectory(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects");
                }
            }
        }
        /// <summary>
        /// Copy vanilla data.wins as much as needed
        /// </summary>
        public static void CopyVanilla()
        {
            string[] vanilla;

            if (Path.GetExtension(@vanilla2) != ".win")
            {
                var winFiles = new List<string>();
                string rootDataWin = Path.Combine(@vanilla2, "data.win");

                if (File.Exists(rootDataWin))
                    winFiles.Add(rootDataWin);

                var directories = Directory.GetDirectories(@vanilla2)
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);

                foreach (var dir in directories)
                {
                    string chapterDataWin = Path.Combine(dir, "data.win");
                    if (File.Exists(chapterDataWin))
                        winFiles.Add(chapterDataWin);
                }

                vanilla = winFiles.ToArray();
            }
            else
            {
                vanilla = new string[] { vanilla2 };
            }

            chapterAmount = vanilla.Length;
            Console.WriteLine($"Total chapters to process: {chapterAmount}");

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    Directory.CreateDirectory(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects");
                }
            }

            File.WriteAllText(@output + "/Cache/running/chapterAmount.txt", Convert.ToString(chapterAmount));

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    string targetPath = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win";
                    FileLinker.LinkOrCopy(@vanilla[chapter], targetPath);
                }
            }
        }
        /// <summary>
        /// The titular function; patches a bunch of mods into data.win files
        /// </summary>
        /// <param name="filepath"></param>
        public static void massPatch(string[] filepath = null)
        {
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                xDeltaFile = new string[(modAmount + 2)];

                if (filepath == null)
                {
                    Console.WriteLine($"Enter patches for Chapter {chapter}:");
                    for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                    {
                        Console.Write($"  Patch for Mod {modNumber - 1}: ");
                        string patch = Console.ReadLine().Replace("\"", "");
                        xDeltaFile[modNumber] = patch;
                    }
                }
                else
                {
                    string chapterMods = filepath[chapter];
                    if (string.IsNullOrEmpty(chapterMods))
                        continue;

                    string[] parts = chapterMods.Split(',');
                    var actualPatches = parts.Skip(2).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

                    for (int i = 0; i < actualPatches.Length && i < modAmount; i++)
                    {
                        xDeltaFile[i + 2] = actualPatches[i];
                    }
                }

                // Test: Parelelize xDelta patching
                int maxParallel = Math.Max(1, XdeltaConcurrency);
                var gate = new SemaphoreSlim(maxParallel);
                var jobs = new List<Task>();

                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    if (string.IsNullOrWhiteSpace(xDeltaFile[modNumber]))
                        continue;

                    int _chapter = chapter;
                    int _modNumber = modNumber;

                    jobs.Add(Task.Run(() =>
                    {
                        gate.Wait();
                        try
                        {
                            string patchFile = xDeltaFile[_modNumber].Trim();

                            if (!File.Exists(patchFile))
                            {
                                Console.WriteLine($"WARNING: Patch file not found: {patchFile}");
                                return;
                            }

                            if (Path.GetExtension(patchFile) == ".csx")
                            {
                                using (var modToolProc = new Process())
                                {
                                    string dataPath = Main.@output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/data.win";
                                    string tmpPath  = Main.@output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/dat.win";

                                    if (OperatingSystem.IsWindows())
                                    {
                                        modToolProc.StartInfo.FileName = Main.@modTool;
                                        modToolProc.StartInfo.Arguments =
                                            "load \"" + dataPath + "\" " +
                                            "--verbose --output \"" + tmpPath + "\" " +
                                            "--scripts \"" + patchFile + "\"";
                                    }
                                    if (OperatingSystem.IsLinux())
                                    {
                                        modToolProc.StartInfo.FileName = "/bin/bash";
                                        modToolProc.StartInfo.Arguments =
                                            "-c \"" + Main.@modTool +
                                            "load '" + dataPath + "' --verbose --output '" + tmpPath + "' --scripts '" + patchFile + "'\"";
                                    }

                                    modToolProc.StartInfo.CreateNoWindow = false;
                                    modToolProc.StartInfo.UseShellExecute = false;
                                    modToolProc.StartInfo.RedirectStandardOutput = true;
                                    modToolProc.Start();

                                    string ProcOutput = modToolProc.StandardOutput.ReadToEnd();
                                    Console.WriteLine(ProcOutput);
                                    modToolProc.WaitForExit();

                                    // Atomically replace after the tool is done
                                    if (File.Exists(tmpPath))
                                    {
                                        try { File.Delete(dataPath); } catch { /* best effort */ }
                                        File.Move(tmpPath, dataPath);
                                    }
                                }
                            }
                            else if (Path.GetExtension(patchFile) == ".win")
                            {
                                File.Copy(patchFile, @output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/data.win", true);
                            }
                            else
                            {
                                lock (ModNumbersCacheLock)
                                {
                                    File.WriteAllText(@output + "/Cache/modNumbersCache.txt", Convert.ToString(_modNumber));
                                }
                                using (var bashProc = new Process())
                                {
                                    string sourceFile = @output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/data.win";
                                    string targetFile = @output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/dat.win";

                                    if (OperatingSystem.IsWindows())
                                    {
                                        bashProc.StartInfo.FileName = Main.@DeltaPatcher;
                                        bashProc.StartInfo.Arguments = $"-v -d -f -s \"{sourceFile}\" \"{patchFile}\" \"{targetFile}\"";
                                    }
                                    if (OperatingSystem.IsLinux())
                                    {
                                        bashProc.StartInfo.FileName = "/bin/bash";
                                        bashProc.StartInfo.Arguments = $"-c \"{@DeltaPatcher} -v -d -f -s '{sourceFile}' '{patchFile}' '{targetFile}'\"";
                                    }
                                    bashProc.StartInfo.CreateNoWindow = false;
                                    bashProc.StartInfo.UseShellExecute = false;
                                    bashProc.StartInfo.RedirectStandardOutput = true;
                                    bashProc.Start();

                                    StreamReader reader = bashProc.StandardOutput;
                                    string ProcOutput = reader.ReadToEnd();
                                    Console.WriteLine(ProcOutput);
                                    bashProc.WaitForExit();
                                }

                                if (File.Exists(@output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/dat.win"))
                                {
                                    File.Delete(@output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/data.win");
                                    File.Move(@output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/dat.win",
                                             @output + "/xDeltaCombiner/" + _chapter + "/" + _modNumber + "/data.win");
                                }
                            }
                            Console.WriteLine($"Patched: {patchFile}");
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
                Task.WaitAll(jobs.ToArray());
            }
            Console.WriteLine("\nMass Patch complete, continue or use the compare command to combine mods");
        }

        /// <summary>
        /// Creates modifiedAssets.txt and exists
        /// </summary>
        public static void modifiedListCreate()
        {
            loadCachedNumbers();
            for (int i = 0; i < chapterAmount; i++)
            {
                if (!File.Exists(@output + "/xDeltaCombiner/" + i + "/1/modifiedAssets.txt"))
                {
                    File.Create(@output + "/xDeltaCombiner/" + i + "/1/modifiedAssets.txt").Close();
                }
            }
        }
        /// <summary>
        /// Dumps game objects from mods
        /// </summary>
        public static void dump()
        {
            loadCachedNumbers();

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                File.WriteAllText(@output + "/Cache/running/chapterNumber.txt", Convert.ToString(chapter));
                Console.WriteLine("Starting dump, this may take up to a minute per mod (and vanilla)");

                for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    // Prepare working tree
                    string workRoot        = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString());
                    string workObjectsRoot = Path.Combine(workRoot, "Objects");
                    string workCodes       = Path.Combine(workObjectsRoot, "CodeEntries");
                    string workSprites     = Path.Combine(workObjectsRoot, "Sprites");
                    string workAssetOrder  = Path.Combine(workObjectsRoot, "AssetOrder.txt");
                    string dataWin         = Path.Combine(workRoot, "data.win");

                    Directory.CreateDirectory(workCodes);
                    Directory.CreateDirectory(workSprites);

                    File.WriteAllText(@output + "/Cache/running/modNumbersCache.txt", Convert.ToString(modNumber));
                    if (modNumber == 1) continue; // original behavior

                    // Legacy slot-keyed cache (back-compat)
                    string slotCacheRoot   = DumpCacheDir(chapter, modNumber);
                    string slotStamp       = DumpStampPath(chapter, modNumber);
                    string slotObjectsRoot = Path.Combine(slotCacheRoot, "Objects");

                    // Toggles
                    bool cacheOn        = CacheEnabled();
                    bool cacheSpritesOn = CacheSpritesEnabled();

                    // === Pre-hash (content of input data.win before UTMT runs) ===
                    string preSig = File.Exists(dataWin) ? HashCache.Sha1Base64(dataWin) : "";

                    // Hash-keyed cache (keyed by PRE hash)
                    string? hashCacheRootByPre = !string.IsNullOrEmpty(preSig) ? DumpCacheDirByHash(chapter, preSig) : null;
                    string? hashStampByPre     = !string.IsNullOrEmpty(preSig) ? DumpStampPathByHash(chapter, preSig) : null;

                    static (string pre, string post) ReadStamp(string path)
                    {
                        try
                        {
                            var txt = File.ReadAllText(path).Trim();
                            string pre = "", post = "";
                            foreach (var l in txt.Split('\n'))
                            {
                                var s = l.Trim();
                                if (s.StartsWith("pre="))  pre  = s.Substring(4);
                                else if (s.StartsWith("post=")) post = s.Substring(5);
                            }
                            if (pre == "" && post == "" && !string.IsNullOrEmpty(txt))
                                pre = txt; // back-compat single-line
                            return (pre, post);
                        }
                        catch { return ("",""); }
                    }

                    bool haveHashCache =
                        cacheOn && !string.IsNullOrEmpty(preSig) &&
                        hashCacheRootByPre != null &&
                        Directory.Exists(Path.Combine(hashCacheRootByPre, "Objects")) &&
                        File.Exists(hashStampByPre!) &&
                        ReadStamp(hashStampByPre!).pre == preSig &&
                        Directory.Exists(Path.Combine(hashCacheRootByPre, "Objects", "CodeEntries")) &&
                        (cacheSpritesOn ? Directory.Exists(Path.Combine(hashCacheRootByPre, "Objects", "Sprites")) : true);

                    bool haveSlotCache =
                        !haveHashCache &&
                        cacheOn && !string.IsNullOrEmpty(preSig) &&
                        Directory.Exists(slotObjectsRoot) &&
                        File.Exists(slotStamp) &&
                        (ReadStamp(slotStamp).pre == preSig) &&
                        Directory.Exists(Path.Combine(slotObjectsRoot, "CodeEntries")) &&
                        (cacheSpritesOn ? Directory.Exists(Path.Combine(slotObjectsRoot, "Sprites")) : true);

                    // Migrate matching legacy slot caches (same PRE) once
                    bool haveMigratedCache = false;
                    string? migratedCacheRoot = null;
                    if (!haveHashCache && !haveSlotCache && cacheOn && !string.IsNullOrEmpty(preSig))
                    {
                        string chapterExportDir = Path.Combine(@output, "Cache", "export", chapter.ToString());
                        if (Directory.Exists(chapterExportDir))
                        {
                            foreach (var dir in Directory.EnumerateDirectories(chapterExportDir))
                            {
                                var name = Path.GetFileName(dir);
                                if (name.Equals("byhash", StringComparison.OrdinalIgnoreCase)) continue;
                                if (!int.TryParse(name, out _)) continue;

                                var stPath  = Path.Combine(dir, ".stamp");
                                var objPath = Path.Combine(dir, "Objects");
                                if (!File.Exists(stPath) || !Directory.Exists(objPath)) continue;

                                try
                                {
                                    if (ReadStamp(stPath).pre == preSig &&
                                        Directory.Exists(Path.Combine(objPath, "CodeEntries")) &&
                                        (cacheSpritesOn ? Directory.Exists(Path.Combine(objPath, "Sprites")) : true))
                                    {
                                        haveMigratedCache = true;
                                        migratedCacheRoot = dir;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    if (haveHashCache || haveSlotCache || haveMigratedCache)
                    {
                        Console.WriteLine($"  Mod {modNumber}: unchanged since last dump, reusing cached export");

                        var srcRoot = haveHashCache ? hashCacheRootByPre! : haveSlotCache ? slotCacheRoot : migratedCacheRoot!;
                        var srcObjects = Path.Combine(srcRoot, "Objects");

                        // Rehydrate Objects/* from cache (optionally skip Sprites to save space)
                        MirrorObjectsSelective(srcObjects, workObjectsRoot, includeSprites: cacheSpritesOn);

                        if (!cacheSpritesOn)
                        {
                            using var proc = new Process();
                            if (OperatingSystem.IsWindows())
                            {
                                proc.StartInfo.FileName  = Main.@modTool;
                                proc.StartInfo.Arguments =
                                    $"load \"{dataWin}\" --verbose --output \"{dataWin}\" " +
                                    $"--scripts \"{Main.@pwd}/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\"";
                            }
                            else
                            {
                                proc.StartInfo.FileName  = "/bin/bash";
                                proc.StartInfo.Arguments =
                                    "-c \"" + Main.@modTool +
                                    $" load '{dataWin}' --verbose --output '{dataWin}' " +
                                    $"--scripts '{Main.@pwd}/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx'\"";
                            }
                            proc.StartInfo.CreateNoWindow = false;
                            proc.StartInfo.UseShellExecute = false;
                            proc.StartInfo.RedirectStandardOutput = true;
                            proc.Start();
                            Console.WriteLine(proc.StandardOutput.ReadToEnd());
                            proc.WaitForExit();

                            string postTex = File.Exists(dataWin) ? HashCache.Sha1Base64(dataWin) : preSig;
                            if (cacheOn && !string.IsNullOrEmpty(preSig))
                            {
                                try { File.WriteAllText(slotStamp, $"pre={preSig}\npost={postTex}\n"); } catch { }
                            }
                        }

                        if (haveMigratedCache)
                        {
                            try
                            {
                                var outRoot = DumpCacheDirByHash(chapter, preSig);
                                Directory.CreateDirectory(Path.Combine(outRoot, "Objects"));
                                MirrorObjectsSelective(srcObjects, Path.Combine(outRoot, "Objects"), includeSprites: true);
                                File.WriteAllText(DumpStampPathByHash(chapter, preSig), $"pre={preSig}\npost=\n");
                            }
                            catch { }
                        }

                        // warnings
                        if (Directory.Exists(workCodes))
                        {
                            var empties = Directory.EnumerateFiles(workCodes, "*.gml")
                                                   .Select(f => new FileInfo(f)).Where(fi => fi.Length == 0).ToList();
                            if (empties.Count > 0)
                            {
                                Console.WriteLine($"  WARNING: {empties.Count} empty GML files found in Mod {modNumber}:");
                                foreach (var ef in empties.Take(5)) Console.WriteLine($"    - {Path.GetFileName(ef.FullName)}");
                            }
                        }
                        if (!File.Exists(workAssetOrder))
                            Console.WriteLine("  WARNING: AssetOrder.txt missing after cache rehydrate");

                        continue;
                    }

                    // No valid cache → run UTMT with 3 export scripts
                    using (var modToolProc = new Process())
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            modToolProc.StartInfo.FileName = Main.@modTool;
                            modToolProc.StartInfo.Arguments =
                                "load \"" + dataWin + "\" --verbose --output \"" + dataWin + "\"" +
                                " --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\"" +
                                " --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllCode.csx\"" +
                                " --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAssetOrder.csx\"";
                        }
                        else
                        {
                            modToolProc.StartInfo.FileName = "/bin/bash";
                            modToolProc.StartInfo.Arguments =
                                "-c \"" + Main.@modTool + "load '" + dataWin + "' --verbose --output '" + dataWin + "'" +
                                " --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx'" +
                                " --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllCode.csx'" +
                                " --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAssetOrder.csx'\"";
                        }
                        modToolProc.StartInfo.CreateNoWindow = false;
                        modToolProc.StartInfo.UseShellExecute = false;
                        modToolProc.StartInfo.RedirectStandardOutput = true;
                        modToolProc.Start();

                        Console.WriteLine(modToolProc.StandardOutput.ReadToEnd());
                        modToolProc.WaitForExit();
                    }

                    // warnings
                    if (Directory.Exists(workCodes))
                    {
                        var empties = Directory.EnumerateFiles(workCodes, "*.gml")
                                               .Select(f => new FileInfo(f)).Where(fi => fi.Length == 0).ToList();
                        if (empties.Count > 0)
                        {
                            Console.WriteLine($"  WARNING: {empties.Count} empty GML files found in Mod {modNumber}:");
                            foreach (var ef in empties.Take(5)) Console.WriteLine($"    - {Path.GetFileName(ef.FullName)}");
                        }
                    }
                    if (!File.Exists(workAssetOrder))
                        Console.WriteLine("  WARNING: AssetOrder.txt missing after dump");

                    // Persist to cache (keyed by PRE; stamp pre+post)
                    if (CacheEnabled())
                    {
                        try
                        {
                            string postSig = File.Exists(dataWin) ? HashCache.Sha1Base64(dataWin) : preSig;
                            if (!string.IsNullOrEmpty(preSig))
                            {
                                var outRoot = DumpCacheDirByHash(chapter, preSig);
                                Directory.CreateDirectory(Path.Combine(outRoot, "Objects"));
                                MirrorObjectsSelective(workObjectsRoot, Path.Combine(outRoot, "Objects"),
                                                       includeSprites: CacheSpritesEnabled());
                                File.WriteAllText(DumpStampPathByHash(chapter, preSig), $"pre={preSig}\npost={postSig}\n");

                                // Back-compat legacy slot stamp
                                try { File.WriteAllText(slotStamp, $"pre={preSig}\npost={postSig}\n"); } catch { }
                            }
                            PruneExportCacheIfNeeded();
                        }
                        catch { }
                    }
                }
            }
        }




        // TEST: Three new helpers for dump().
        static long DirSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long total = 0;
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                try { total += new FileInfo(f).Length; } catch { }
            return total;
        }

        static void PruneExportCacheIfNeeded()
        {
            // 0 disables cache; default 2048 MB
            if (!int.TryParse(Environment.GetEnvironmentVariable("GM3P_EXPORT_CACHE_CAP_MB"), out var capMb))
                capMb = 2048;
            if (capMb <= 0) return;

            string root = Path.Combine(@output, "Cache", "exports");
            if (!Directory.Exists(root)) return;

            long capBytes = (long)capMb * 1024 * 1024;
            long used = DirSize(root);
            if (used <= capBytes) return;

            // Collect entries <chapter>/<mod> with last access (stamp mtime) and size
            var entries = new List<(string path, DateTime last, long size)>();
            foreach (var chapterDir in Directory.EnumerateDirectories(root))
            foreach (var modDir in Directory.EnumerateDirectories(chapterDir))
            {
                string stamp = Path.Combine(modDir, "dump.sha1");
                DateTime last = File.Exists(stamp) ? new FileInfo(stamp).LastWriteTimeUtc : Directory.GetLastWriteTimeUtc(modDir);
                long size = DirSize(modDir);
                entries.Add((modDir, last, size));
            }

            // Oldest first; delete until under cap
            foreach (var e in entries.OrderBy(t => t.last))
            {
                try { Directory.Delete(e.path, recursive: true); } catch { }
                used -= e.size;
                if (used <= capBytes) break;
            }
        }

        // Mirrors Objects/*; optionally skips Sprites/*
        static void MirrorObjectsSelective(string srcObjects, string dstObjects, bool includeSprites)
        {
            if (!Directory.Exists(srcObjects)) return;
            Directory.CreateDirectory(dstObjects);

            // create dirs
            foreach (var dir in Directory.EnumerateDirectories(srcObjects, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(srcObjects, dir);
                if (!includeSprites && rel.StartsWith("Sprites", StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.CreateDirectory(Path.Combine(dstObjects, rel));
            }

            // copy/link files
            foreach (var file in Directory.EnumerateFiles(srcObjects, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(srcObjects, file);
                if (!includeSprites && rel.StartsWith("Sprites", StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = Path.Combine(dstObjects, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                // Use LinkOrCopyAtomic to ensure atomicity and avoid stale files
                // This will either link or copy the file, depending on the platform and capabilities
                FileLinker.LinkOrCopy(file, target);
            }
        }




        /// <summary>
        /// The main draw (marketing-wise) of GM3P, compares and combines the vanilla game files with the mod files.
        /// </summary>
        public static void CompareCombine()
        {
            loadCachedNumbers();
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                Console.WriteLine(chapter == 0 ? "Processing Root Chapter:" : $"Processing Chapter {chapter}:");

                int  changedThisChapter = 0;
                bool anyCodeChanged     = false;
                bool anySpriteChanged   = false;
                bool assetOrderChanged  = false;

                var chapterModified = new List<string>();

                string vanillaObjectsPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "0", "Objects");
                if (!Directory.Exists(vanillaObjectsPath))
                {
                    Console.WriteLine($"  WARNING: No vanilla Objects folder for chapter {chapter}");
                    continue;
                }

                // clear stale merged files
                string mergedObjectsPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects");
                if (Directory.Exists(mergedObjectsPath)) { try { Directory.Delete(mergedObjectsPath, true); } catch { } }
                Directory.CreateDirectory(mergedObjectsPath);

                var vanillaFiles = Directory.GetFiles(vanillaObjectsPath, "*", SearchOption.AllDirectories);
                Console.WriteLine($"  Found {vanillaFiles.Length} vanilla files");

                // vanilla map keyed by relative path under Objects/
                var vanillaFileDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // collect versions keyed by relative path
                var allFileVersions = new Dictionary<string, List<ModFileInfo>>(StringComparer.OrdinalIgnoreCase);
                var allKnown        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // add vanilla versions
                foreach (var vf in vanillaFiles)
                {
                    string relKey = Path.GetRelativePath(vanillaObjectsPath, vf).Replace('\\','/');
                    vanillaFileDict[relKey] = vf;

                    allKnown.Add(relKey);
                    if (!allFileVersions.TryGetValue(relKey, out var list))
                    {
                        list = new List<ModFileInfo>();
                        allFileVersions[relKey] = list;
                    }
                    list.Add(new ModFileInfo { ModNumber = 0, FilePath = vf, ModName = "Vanilla" });
                }

                // add mod versions
                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    string modObjectsPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "Objects");
                    if (!Directory.Exists(modObjectsPath)) continue;

                    var modFiles = Directory.GetFiles(modObjectsPath, "*", SearchOption.AllDirectories);
                    Console.WriteLine($"  Mod {modNumber - 1} has {modFiles.Length} files");

                    foreach (var mf in modFiles)
                    {
                        string relKey = Path.GetRelativePath(modObjectsPath, mf).Replace('\\','/');
                        allKnown.Add(relKey);

                        if (!allFileVersions.TryGetValue(relKey, out var list))
                        {
                            list = new List<ModFileInfo>();
                            allFileVersions[relKey] = list;
                        }
                        list.Add(new ModFileInfo { ModNumber = modNumber, FilePath = mf, ModName = $"Mod {modNumber - 1}" });
                    }
                }

                Console.WriteLine($"Chapter {chapter}: Found {allKnown.Count} unique files across vanilla and {Main.modAmount} mod(s)");

                // process everything except AssetOrder.txt
                foreach (string relKey in allKnown)
                {
                    if (relKey.Equals("AssetOrder.txt", StringComparison.OrdinalIgnoreCase)) continue;

                    var versions       = allFileVersions[relKey];
                    var vanillaVersion = versions.FirstOrDefault(v => v.ModNumber == 0);
                    var modVersions    = versions.Where(v => v.ModNumber > 0).ToList();

                    // compute diffs vs vanilla
                    var different = new List<ModFileInfo>();
                    if (vanillaVersion != null && modVersions.Count > 0)
                    {
                        string ext = Path.GetExtension(relKey);
                        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var mv in modVersions)
                            {
                                bool diff = AreSpritesDifferent(mv.FilePath, vanillaVersion.FilePath);
                                if (diff) different.Add(mv);
                            }
                        }
                        else
                        {
                            string vHash = HashCache.Sha1Base64(vanillaVersion.FilePath);
                            foreach (var mv in modVersions)
                            {
                                string mHash = HashCache.Sha1Base64(mv.FilePath);
                                if (!string.Equals(vHash, mHash, StringComparison.Ordinal))
                                    different.Add(mv);
                            }
                        }
                    }
                    else if (vanillaVersion == null && modVersions.Count > 0)
                    {
                        different = modVersions;
                    }

                    // if unchanged, DO NOT copy vanilla into merged/Objects
                    if (different.Count == 0) continue;

                    string targetPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", relKey);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                    if (different.Count == 1)
                    {
                        var w = different[0];
                        File.Copy(w.FilePath, targetPath, true);

                        string hash = HashCache.Sha1Base64(targetPath);
                        Main.modifiedAssets.Add(relKey + "        " + hash);
                        chapterModified.Add(relKey + "        " + hash);

                        changedThisChapter++;
                        string ext = Path.GetExtension(relKey);
                        if (relKey.StartsWith("Sprites/", StringComparison.OrdinalIgnoreCase) || ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                            anySpriteChanged = true;
                        if (relKey.Contains("/CodeEntries/", StringComparison.OrdinalIgnoreCase) || ext.Equals(".gml", StringComparison.OrdinalIgnoreCase))
                            anyCodeChanged = true;
                    }
                    else // >1 different
                    {
                        string ext = Path.GetExtension(relKey).ToLowerInvariant();
                        if (ext == ".png")
                        {
                            var best = SelectBestSprite(different, vanillaVersion);
                            File.Copy(best.FilePath, targetPath, true);
                            anySpriteChanged = true; changedThisChapter++;
                        }
                        else if (ext == ".ogg" || ext == ".wav" || ext == ".mp3")
                        {
                            var last = different.Last();
                            File.Copy(last.FilePath, targetPath, true);
                            changedThisChapter++;
                        }
                        else
                        {
                            bool ok = false;
                            if (vanillaVersion != null)
                                ok = PerformSimpleMerge(vanillaVersion.FilePath, different, targetPath);
                            if (!ok) File.Copy(different[0].FilePath, targetPath, true);

                            string hash = HashCache.Sha1Base64(targetPath);
                            Main.modifiedAssets.Add(relKey + "        " + hash);
                            chapterModified.Add(relKey + "        " + hash);

                            anyCodeChanged = true; changedThisChapter++;
                        }
                    }
                }

                // AssetOrder handling + change detect
                HandleAssetOrderFile(chapter, allFileVersions, vanillaFileDict);
                try
                {
                    var mergedAO = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", "AssetOrder.txt");
                    if (File.Exists(mergedAO) && vanillaFileDict.TryGetValue("AssetOrder.txt", out var vanillaAO))
                    {
                        bool same = false;
                        var fi1 = new FileInfo(mergedAO); var fi2 = new FileInfo(vanillaAO);
                        if (fi1.Exists && fi2.Exists && fi1.Length == fi2.Length)
                        {
                            using var f1 = File.OpenRead(mergedAO);
                            using var f2 = File.OpenRead(vanillaAO);
                            same = SHA1.Create().ComputeHash(f1).SequenceEqual(SHA1.Create().ComputeHash(f2));
                        }
                        if (same) { try { File.Delete(mergedAO); } catch { } }
                        else
                        {
                            assetOrderChanged = true; changedThisChapter++;
                            string aoHash = HashCache.Sha1Base64(mergedAO);
                            chapterModified.Add("AssetOrder.txt        " + aoHash);
                            Main.modifiedAssets.Add("AssetOrder.txt        " + aoHash);
                        }
                    }
                }
                catch { }

                Console.WriteLine("\nValidating sprites after merge...");
                ValidateSprites(chapter);

                // stamp for import gating
                var stampDir = Path.Combine(@output, "Cache", "running");
                Directory.CreateDirectory(stampDir);
                File.WriteAllText(Path.Combine(stampDir, $"chapter_{chapter}_changes.txt"),
                    $"{changedThisChapter}|{(anyCodeChanged?1:0)}|{(anySpriteChanged?1:0)}|{(assetOrderChanged?1:0)}");

                // write per-chapter modified list
                var modListPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "modifiedAssets.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(modListPath)!);
                File.WriteAllLines(modListPath, chapterModified);
            }

            Main.combined = true;
        }





        /// <summary>
        /// New simple merge that preserves content
        /// </summary>
        /// <param name="baseFile"></param>
        /// <param name="mods"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        private static bool PerformSimpleMerge(string baseFile, List<ModFileInfo> mods, string outputFile)
        {
            try
            {
                // Read base content
                string baseContent = File.ReadAllText(baseFile);

                // If only one mod, just check if it's different from base
                if (mods.Count == 1)
                {
                    string modContent = File.ReadAllText(mods[0].FilePath);
                    if (modContent != baseContent)
                    {
                        File.WriteAllText(outputFile, modContent);
                    }
                    else
                    {
                        File.WriteAllText(outputFile, baseContent);
                    }
                    return true;
                }

                // For multiple mods, try git merge first
                string gitPath = FindGit();
                if (!string.IsNullOrEmpty(gitPath))
                {
                    bool gitSuccess = PerformGitMerge(baseFile, mods, outputFile);

                    // Verify the result
                    if (gitSuccess && File.Exists(outputFile))
                    {
                        var info = new FileInfo(outputFile);
                        if (info.Length > 0)
                        {
                            return true;
                        }
                    }
                }

                // Fallback: intelligent concatenation
                var lines = new List<string>();
                var addedLines = new HashSet<string>();

                // Start with base
                lines.AddRange(baseContent.Split('\n'));
                foreach (var line in lines)
                {
                    addedLines.Add(line.Trim());
                }

                // Add unique content from each mod
                foreach (var mod in mods)
                {
                    string modContent = File.ReadAllText(mod.FilePath);
                    var modLines = modContent.Split('\n');

                    foreach (var line in modLines)
                    {
                        string trimmedLine = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine) && !addedLines.Contains(trimmedLine))
                        {
                            lines.Add(line);
                            addedLines.Add(trimmedLine);
                        }
                    }
                }

                File.WriteAllText(outputFile, string.Join("\n", lines));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Simple merge error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Simplified git merge with better error handling and debugging
        /// </summary>
        /// <param name="baseFile"></param>
        /// <param name="mods"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        private static bool PerformGitMerge(string baseFile, List<ModFileInfo> mods, string outputFile)
        {
            try
            {
                string gitPath = FindGit();
                if (string.IsNullOrEmpty(gitPath))
                {
                    Console.WriteLine("    Git not available, using fallback merge");
                    return false;
                }

                // For debugging
                Console.WriteLine($"    Using git merge with {mods.Count} mod(s)");

                string tempRepo = Path.Combine(Path.GetTempPath(), $"GM3P_{Guid.NewGuid():N}".Substring(0, 8));
                Directory.CreateDirectory(tempRepo);

                try
                {
                    string workFile = Path.Combine(tempRepo, "file.txt");

                    // Initialize git repo with minimal config
                    var initResult = RunGitCommand(gitPath, tempRepo, "init -q");
                    if (initResult == null)
                    {
                        Console.WriteLine("    Git init failed");
                        return false;
                    }

                    RunGitCommand(gitPath, tempRepo, "config user.email \"gm3p@local\"");
                    RunGitCommand(gitPath, tempRepo, "config user.name \"GM3P\"");
                    RunGitCommand(gitPath, tempRepo, "config core.autocrlf false");
                    RunGitCommand(gitPath, tempRepo, "config merge.ours.driver \"true\""); // Helps with conflicts

                    // Create base commit
                    File.Copy(baseFile, workFile, true);
                    RunGitCommand(gitPath, tempRepo, "add .");
                    var baseCommit = RunGitCommand(gitPath, tempRepo, "commit -q -m \"base\" --allow-empty");

                    if (baseCommit == null)
                    {
                        Console.WriteLine("    Failed to create base commit");
                        return false;
                    }

                    // Create branches for each mod
                    var branches = new List<string>();
                    for (int i = 0; i < mods.Count; i++)
                    {
                        string branchName = $"m{i}";
                        branches.Add(branchName);

                        var checkoutResult = RunGitCommand(gitPath, tempRepo, $"checkout -q -b {branchName} master");
                        if (checkoutResult == null)
                        {
                            Console.WriteLine($"    Failed to create branch {branchName}");
                            continue;
                        }

                        // Copy mod file content
                        File.Copy(mods[i].FilePath, workFile, true);

                        // Verify file has content
                        var fileInfo = new FileInfo(workFile);
                        Console.WriteLine($"    Mod {i} file size: {fileInfo.Length} bytes");

                        RunGitCommand(gitPath, tempRepo, "add .");
                        RunGitCommand(gitPath, tempRepo, $"commit -q -m \"mod{i}\" --allow-empty");
                        RunGitCommand(gitPath, tempRepo, "checkout -q master");
                    }

                    // Perform merge
                    string branchList = string.Join(" ", branches);
                    Console.WriteLine($"    Merging branches: {branchList}");

                    var mergeResult = RunGitCommand(gitPath, tempRepo, $"merge {branchList} --no-edit -m \"merged\"", true);

                    // Check if merge succeeded or has conflicts
                    if (mergeResult != null)
                    {
                        if (mergeResult.Contains("CONFLICT"))
                        {
                            Console.WriteLine("    Merge has conflicts, attempting auto-resolution");

                            // Try to auto-resolve by taking all changes
                            RunGitCommand(gitPath, tempRepo, "add .", true);
                            RunGitCommand(gitPath, tempRepo, "commit --no-edit -m \"resolved\"", true);
                        }
                    }

                    // Copy result
                    if (File.Exists(workFile))
                    {
                        string content = File.ReadAllText(workFile);

                        // Check for conflicts
                        if (content.Contains("<<<<<<<"))
                        {
                            Console.WriteLine("    Auto-resolving conflict markers");
                            content = AutoResolveConflicts(content);
                        }

                        // Verify content isn't empty
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            Console.WriteLine("    WARNING: Merged content is empty!");
                            return false;
                        }

                        File.WriteAllText(outputFile, content);

                        var outputInfo = new FileInfo(outputFile);
                        Console.WriteLine($"    Merge complete, output size: {outputInfo.Length} bytes");

                        return outputInfo.Length > 0;
                    }

                    Console.WriteLine("    Work file not found after merge");
                    return false;
                }
                finally
                {
                    try
                    {
                        Directory.Delete(tempRepo, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Git merge exception: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Runs a command through Git
        /// </summary>
        /// <param name="gitPath"></param>
        /// <param name="workingDir"></param>
        /// <param name="arguments"></param>
        /// <param name="allowNonZeroExit"></param>
        /// <returns></returns>
        public static string RunGitCommand(string gitPath, string workingDir, string arguments, bool allowNonZeroExit = false)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = gitPath;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDir;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    bool exited = process.WaitForExit(10000); // 10 second timeout

                    if (!exited)
                    {
                        Console.WriteLine($"      Git command timed out: {arguments}");
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    if (process.ExitCode != 0 && !allowNonZeroExit)
                    {
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine($"      Git error: {error}");
                        }
                        return null;
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      Git command exception: {ex.Message}");
                return null;
            }
        }

        private static string AutoResolveConflicts(string content)
        {
            var conflictPattern = new System.Text.RegularExpressions.Regex(
                @"<<<<<<< .*?\n(.*?)\n=======\n(.*?)\n>>>>>>> .*?\n",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            return conflictPattern.Replace(content, (match) =>
            {
                string local = match.Groups[1].Value;
                string remote = match.Groups[2].Value;

                if (string.IsNullOrWhiteSpace(local))
                    return remote;
                if (string.IsNullOrWhiteSpace(remote))
                    return local;

                // Keep both changes
                return local + "\n" + remote;
            });
        }

        /// <summary>
        /// Improved AssetOrder handling. Now it treats it as a special case.
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="allFileVersions"></param>
        /// <param name="vanillaFileDict"></param>
        private static void HandleAssetOrderFile(int chapter, Dictionary<string, List<ModFileInfo>> allFileVersions, Dictionary<string, string> vanillaFileDict)
        {
            Console.WriteLine("\n=== Processing AssetOrder.txt ===");

            string assetOrderFile = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", "AssetOrder.txt");

            // Check if any mod has AssetOrder.txt
            if (!allFileVersions.ContainsKey("AssetOrder.txt"))
            {
                // No mods have AssetOrder.txt, just copy vanilla if it exists
                if (vanillaFileDict.ContainsKey("AssetOrder.txt"))
                {
                    Console.WriteLine("  No mods modify AssetOrder.txt, using vanilla");
                    File.Copy(vanillaFileDict["AssetOrder.txt"], assetOrderFile, true);
                }
                else
                {
                    Console.WriteLine("  WARNING: No AssetOrder.txt found in vanilla or mods!");
                }
                return;
            }

            var assetOrderVersions = allFileVersions["AssetOrder.txt"];
            var vanillaAssetOrder = assetOrderVersions.FirstOrDefault(v => v.ModNumber == 0);
            var modAssetOrders = assetOrderVersions.Where(v => v.ModNumber > 0).ToList();

            // Check which mod versions are actually different from vanilla
            var differentModAssetOrders = new List<ModFileInfo>();

            if (vanillaAssetOrder != null)
            {
                var vanillaLines = File.ReadAllLines(vanillaAssetOrder.FilePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                foreach (var mod in modAssetOrders)
                {
                    try
                    {
                        var modLines = File.ReadAllLines(mod.FilePath)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

                        // Check if content or order is different
                        bool isDifferent = false;

                        // Different count means definitely different
                        if (vanillaLines.Count != modLines.Count)
                        {
                            isDifferent = true;
                        }
                        else
                        {
                            // Check if order is different
                            for (int i = 0; i < vanillaLines.Count; i++)
                            {
                                if (vanillaLines[i] != modLines[i])
                                {
                                    isDifferent = true;
                                    break;
                                }
                            }
                        }

                        if (isDifferent)
                        {
                            differentModAssetOrders.Add(mod);
                            Console.WriteLine($"  {mod.ModName} modifies AssetOrder.txt");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERROR reading {mod.ModName}'s AssetOrder.txt: {ex.Message}");
                        differentModAssetOrders.Add(mod);
                    }
                }
            }
            else
            {
                // No vanilla AssetOrder.txt
                differentModAssetOrders = modAssetOrders;
                Console.WriteLine("  WARNING: No vanilla AssetOrder.txt found!");
            }

            // Process based on number of different versions
            if (differentModAssetOrders.Count == 0 && vanillaAssetOrder != null)
            {
                // No mods change it, use vanilla
                File.Copy(vanillaAssetOrder.FilePath, assetOrderFile, true);
                Console.WriteLine("  ✓ Using vanilla AssetOrder.txt (no mods modify it)");
            }
            else if (differentModAssetOrders.Count == 1)
            {
                // Only one mod changes it, use that mod's version
                File.Copy(differentModAssetOrders[0].FilePath, assetOrderFile, true);
                Console.WriteLine($"  ✓ Using {differentModAssetOrders[0].ModName}'s AssetOrder.txt (only mod that changes it)");
            }
            else if (differentModAssetOrders.Count > 1)
            {
                // Multiple mods change AssetOrder.txt - need intelligent merging
                Console.WriteLine($"Merging AssetOrder.txt from {differentModAssetOrders.Count} mods...");

                try
                {
                    var mergedAssets = MergeAssetOrderIntelligently(
                        vanillaAssetOrder?.FilePath,
                        differentModAssetOrders
                    );

                    File.WriteAllLines(assetOrderFile, mergedAssets);
                    Console.WriteLine($"  ✓ Merged AssetOrder.txt: {mergedAssets.Count} total assets");

                    // Log details about the merge
                    if (vanillaAssetOrder != null)
                    {
                        var vanillaCount = File.ReadAllLines(vanillaAssetOrder.FilePath)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Count();

                        int newAssets = mergedAssets.Count - vanillaCount;
                        if (newAssets > 0)
                        {
                            Console.WriteLine($"    Added {newAssets} new assets from mods");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: Failed to merge AssetOrder.txt: {ex.Message}");
                    Console.WriteLine($"  Falling back to last mod's version");

                    // Fallback: use the last mod's version
                    var lastMod = differentModAssetOrders.Last();
                    File.Copy(lastMod.FilePath, assetOrderFile, true);
                }
            }

            // Validate the final AssetOrder.txt
            ValidateAssetOrder(assetOrderFile, chapter);
        }

        /// <summary>
        /// New method for intelligent AssetOrder merging
        /// </summary>
        /// <param name="vanillaPath"></param>
        /// <param name="modVersions"></param>
        /// <returns></returns>
        private static List<string> MergeAssetOrderIntelligently(string vanillaPath, List<ModFileInfo> modVersions)
        {
            var result = new List<string>();
            var addedAssets = new HashSet<string>();

            // Start with vanilla order if available
            List<string> vanillaOrder = null;
            if (!string.IsNullOrEmpty(vanillaPath) && File.Exists(vanillaPath))
            {
                vanillaOrder = File.ReadAllLines(vanillaPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // Add vanilla assets first, preserving their order
                foreach (var asset in vanillaOrder)
                {
                    result.Add(asset);
                    addedAssets.Add(asset);
                }

                Console.WriteLine($"    Base: {vanillaOrder.Count} vanilla assets");
            }

            // Process each mod's additions
            foreach (var mod in modVersions)
            {
                var modAssets = File.ReadAllLines(mod.FilePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                int newAssetsFromMod = 0;

                // Strategy: Add new assets in the position where the mod places them
                for (int i = 0; i < modAssets.Count; i++)
                {
                    string asset = modAssets[i];

                    if (!addedAssets.Contains(asset))
                    {
                        // This is a new asset from this mod

                        // Try to find a good position for it
                        int insertPosition = FindBestInsertPosition(
                            result,
                            modAssets,
                            i,
                            asset
                        );

                        if (insertPosition >= 0 && insertPosition <= result.Count)
                        {
                            result.Insert(insertPosition, asset);
                        }
                        else
                        {
                            // Fallback: add to end
                            result.Add(asset);
                        }

                        addedAssets.Add(asset);
                        newAssetsFromMod++;
                    }
                }

                if (newAssetsFromMod > 0)
                {
                    Console.WriteLine($"    {mod.ModName}: added {newAssetsFromMod} new assets");
                }
            }

            // Check for any reordering conflicts
            DetectAndLogOrderingConflicts(vanillaOrder, modVersions, result);

            return result;
        }

        /// <summary>
        /// Helper method to find the best position to insert a new asset
        /// </summary>
        /// <param name="currentOrder"></param>
        /// <param name="modOrder"></param>
        /// <param name="modIndex"></param>
        /// <param name="newAsset"></param>
        /// <returns></returns>
        private static int FindBestInsertPosition(
            List<string> currentOrder,
            List<string> modOrder,
            int modIndex,
            string newAsset)
        {
            // Look for context - what comes before and after in the mod's order
            string previousAsset = null;
            string nextAsset = null;

            // Find previous asset that's already in our list
            for (int i = modIndex - 1; i >= 0; i--)
            {
                if (currentOrder.Contains(modOrder[i]))
                {
                    previousAsset = modOrder[i];
                    break;
                }
            }

            // Find next asset that's already in our list
            for (int i = modIndex + 1; i < modOrder.Count; i++)
            {
                if (currentOrder.Contains(modOrder[i]))
                {
                    nextAsset = modOrder[i];
                    break;
                }
            }

            // Determine insert position based on context
            if (previousAsset != null)
            {
                int prevIndex = currentOrder.IndexOf(previousAsset);
                if (nextAsset != null)
                {
                    int nextIndex = currentOrder.IndexOf(nextAsset);
                    // Insert between previous and next if they're consecutive
                    if (nextIndex == prevIndex + 1)
                    {
                        return nextIndex;
                    }
                }
                // Insert after previous
                return prevIndex + 1;
            }
            else if (nextAsset != null)
            {
                // Insert before next
                return currentOrder.IndexOf(nextAsset);
            }

            // No context found, add to end
            return currentOrder.Count;
        }

        /// <summary>
        /// Helper method to detect ordering conflicts between mods
        /// </summary>
        /// <param name="vanillaOrder"></param>
        /// <param name="modVersions"></param>
        /// <param name="mergedOrder"></param>
        private static void DetectAndLogOrderingConflicts(
            List<string> vanillaOrder,
            List<ModFileInfo> modVersions,
            List<string> mergedOrder)
        {
            if (vanillaOrder == null || vanillaOrder.Count == 0)
                return;

            // Check if any vanilla assets have been reordered
            var vanillaPositions = new Dictionary<string, int>();
            for (int i = 0; i < vanillaOrder.Count; i++)
            {
                vanillaPositions[vanillaOrder[i]] = i;
            }

            var mergedPositions = new Dictionary<string, int>();
            for (int i = 0; i < mergedOrder.Count; i++)
            {
                if (vanillaPositions.ContainsKey(mergedOrder[i]))
                {
                    mergedPositions[mergedOrder[i]] = i;
                }
            }

            // Check for order violations
            int reorderedCount = 0;
            foreach (var asset in vanillaOrder)
            {
                if (!mergedPositions.ContainsKey(asset))
                {
                    Console.WriteLine($"    WARNING: Vanilla asset '{asset}' is missing from merged order!");
                    continue;
                }

                // Check if relative order is preserved
                int vanillaPos = vanillaPositions[asset];
                int mergedPos = mergedPositions[asset];

                // This is a simple check - could be made more sophisticated
                bool wasReordered = false;

                // Check against other vanilla assets
                foreach (var otherAsset in vanillaOrder)
                {
                    if (asset == otherAsset) continue;
                    if (!mergedPositions.ContainsKey(otherAsset)) continue;

                    int otherVanillaPos = vanillaPositions[otherAsset];
                    int otherMergedPos = mergedPositions[otherAsset];

                    // Check if relative order changed
                    bool vanillaBefore = vanillaPos < otherVanillaPos;
                    bool mergedBefore = mergedPos < otherMergedPos;

                    if (vanillaBefore != mergedBefore)
                    {
                        wasReordered = true;
                        break;
                    }
                }

                if (wasReordered)
                {
                    reorderedCount++;
                }
            }

            if (reorderedCount > 0)
            {
                Console.WriteLine($"    ⚠ {reorderedCount} vanilla assets were reordered by mods");
            }
        }

        /// <summary>
        /// New validation method for AssetOrder.txt
        /// </summary>
        /// <param name="assetOrderPath"></param>
        /// <param name="chapter"></param>
        private static void ValidateAssetOrder(string assetOrderPath, int chapter)
        {
            if (!File.Exists(assetOrderPath))
            {
                Console.WriteLine("  ERROR: AssetOrder.txt doesn't exist after merge!");
                return;
            }

            var lines = File.ReadAllLines(assetOrderPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine("  ERROR: AssetOrder.txt is empty!");
                return;
            }

            // Check for duplicates
            var duplicates = lines.GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                Console.WriteLine($"  WARNING: {duplicates.Count} duplicate entries in AssetOrder.txt:");
                foreach (var dup in duplicates.Take(5))
                {
                    Console.WriteLine($"    - {dup}");
                }
            }

            // Check if critical assets are present (especially sprites that might affect animations)
            var spriteAssets = lines.Where(l => l.Contains("spr_") || l.Contains("Sprite")).ToList();
            Console.WriteLine($"  ✓ AssetOrder.txt validated: {lines.Count} total assets, {spriteAssets.Count} sprite-related");

            var animationGroups = lines
                .Select((line, index) => new { Line = line, Index = index })
                .Where(x => x.Line.Contains("spr_") || x.Line.Contains("Sprite"))
                .GroupBy(x => System.Text.RegularExpressions.Regex.Replace(x.Line, @"_\d+$", ""))
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in animationGroups)
            {
                var indices = group.Select(x => x.Index).ToList();
                bool isSequential = true;
                for (int i = 1; i < indices.Count; i++)
                {
                    if (indices[i] != indices[i-1] + 1)
                    {
                        isSequential = false;
                        break;
                    }
                }

                if (!isSequential)
                {
                    Console.WriteLine($"    ⚠ Animation frames not sequential: {group.Key}");
                }
            }

            // Check for any obviously misplaced entries
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                // Check for common issues
                if (line.Contains("\\") || line.Contains("/"))
                {
                    Console.WriteLine($"  WARNING: Line {i + 1} contains path separators: {line}");
                }

                if (line.StartsWith(" ") || line.EndsWith(" "))
                {
                    Console.WriteLine($"  WARNING: Line {i + 1} has leading/trailing spaces: '{line}'");
                }
            }
        }

        public static void HandleNewObjects()
        {
            loadCachedNumbers();
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                Console.WriteLine($"Checking for new objects in chapter {chapter}...");

                HashSet<string> vanillaObjects = new HashSet<string>();
                string vanillaCodePath = @output + "/xDeltaCombiner/" + chapter + "/0/Objects/CodeEntries/";

                if (Directory.Exists(vanillaCodePath))
                {
                    foreach (string file in Directory.GetFiles(vanillaCodePath, "gml_Object_*"))
                    {
                        string filename = Path.GetFileName(file);
                        if (filename.StartsWith("gml_Object_"))
                        {
                            string objectName = ExtractObjectName(filename);
                            if (!string.IsNullOrEmpty(objectName))
                                vanillaObjects.Add(objectName);
                        }
                    }
                }

                for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
                {
                    HashSet<string> newObjects = new HashSet<string>();
                    string modCodePath = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/CodeEntries/";

                    if (Directory.Exists(modCodePath))
                    {
                        foreach (string file in Directory.GetFiles(modCodePath, "gml_Object_*"))
                        {
                            string filename = Path.GetFileName(file);
                            if (filename.StartsWith("gml_Object_"))
                            {
                                string objectName = ExtractObjectName(filename);
                                if (!string.IsNullOrEmpty(objectName) && !vanillaObjects.Contains(objectName))
                                    newObjects.Add(objectName);
                            }
                        }
                    }

                    if (newObjects.Count > 0)
                    {
                        Console.WriteLine($"Mod {modNumber - 1} adds {newObjects.Count} new objects: {string.Join(", ", newObjects)}");
                        string newObjectsFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/NewObjects.txt";
                        File.WriteAllLines(newObjectsFile, newObjects);
                    }
                }
            }
        }
        /// <summary>
        /// Groups GML objects that start with "gml_Object_" by name
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static string ExtractObjectName(string filename)
        {
            int startIndex = "gml_Object_".Length;
            string[] eventTypes = { "_Create_", "_Step_", "_Draw_", "_Alarm_", "_Destroy_", "_Collision_", "_Other_", "_PreCreate_" };

            foreach (string eventType in eventTypes)
            {
                int endIndex = filename.IndexOf(eventType);
                if (endIndex > startIndex)
                {
                    return filename.Substring(startIndex, endIndex - startIndex);
                }
            }

            return null;
        }

        public static void importWithNewObjects()
        {
            loadCachedNumbers();

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                if (Main.modTool == "skip") { Console.WriteLine("Manual import mode..."); continue; }

                Console.WriteLine($"Processing Chapter {chapter}...");

                string workingDataWin = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "data.win");

                // --- Discover mods that declare new objects (unchanged message content) ---
                var modsWithNewObjects = new Dictionary<int, List<string>>();
                for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
                {
                    string newObjectsFile = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "Objects", "NewObjects.txt");
                    if (!File.Exists(newObjectsFile)) continue;

                    var lines = File.ReadAllLines(newObjectsFile)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrWhiteSpace(l))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();

                    if (lines.Count > 0)
                    {
                        modsWithNewObjects[modNumber] = lines;
                        Console.WriteLine($"  Mod {modNumber - 1} adds {lines.Count} new objects");
                    }
                }

                // --- Choose base data.win ---
                if (modsWithNewObjects.Count == 0)
                {
                    Console.WriteLine("No new objects, using vanilla as base");
                    FileLinker.LinkOrCopy(
                        Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "0", "data.win"),
                        workingDataWin);
                }
                else
                {
                    // Validate which mod actually has GML for the declared objects
                    var validated = new Dictionary<int, (int hits, int declared)>();
                    foreach (var kv in modsWithNewObjects)
                    {
                        string codeFolder = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), kv.Key.ToString(), "Objects", "CodeEntries");
                        int hits = 0;

                        if (Directory.Exists(codeFolder))
                        {
                            foreach (var obj in kv.Value)
                            {
                                // any event file of that object counts as a hit
                                string pattern = $"gml_Object_{obj}_*.gml";
                                if (Directory.EnumerateFiles(codeFolder, pattern, SearchOption.TopDirectoryOnly).Any())
                                    hits++;
                            }
                        }

                        validated[kv.Key] = (hits, kv.Value.Count);
                    }

                    int baseModNumber = validated
                        .OrderByDescending(p => p.Value.hits)     // most actual object files present
                        .ThenByDescending(p => p.Value.declared)  // then most declared
                        .ThenByDescending(p => p.Key)             // stable tie-break
                        .First().Key;

                    Console.WriteLine(modsWithNewObjects.Count == 1
                        ? $"  Using Mod {baseModNumber - 1}'s data.win as base"
                        : $"  Using Mod {baseModNumber - 1}'s data.win (has most validated new objects)");

                    FileLinker.LinkOrCopy(
                        Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), baseModNumber.ToString(), "data.win"),
                        workingDataWin);
                }

                // --- Keep messages the same ---
                Console.WriteLine("Importing merged graphics...");
                Console.WriteLine("Importing merged code...");
                Console.WriteLine("Importing AssetOrder...");

                // --- Read change flags (from CompareCombine stamp) ---
                var stamp = Path.Combine(@output, "Cache", "running", $"chapter_{chapter}_changes.txt");
                bool code = false, sprites = false, asset = false;
                if (File.Exists(stamp))
                {
                    var p = File.ReadAllText(stamp).Split('|');
                    code    = p.Length > 1 && p[1] == "1";
                    sprites = p.Length > 2 && p[2] == "1";
                    asset   = p.Length > 3 && p[3] == "1";
                }

                // --- Fallback to filesystem truth if the stamp missed something ---
                string mergedRoot    = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects");
                string mergedSprites = Path.Combine(mergedRoot, "Sprites");
                string mergedCode    = Path.Combine(mergedRoot, "CodeEntries");
                string mergedAO      = Path.Combine(mergedRoot, "AssetOrder.txt");

                if (!sprites && Directory.Exists(mergedSprites) &&
                    Directory.EnumerateFiles(mergedSprites, "*.png", SearchOption.AllDirectories).Any())
                    sprites = true;

                if (!code && Directory.Exists(mergedCode) &&
                    Directory.EnumerateFiles(mergedCode, "*.gml", SearchOption.AllDirectories).Any())
                    code = true;

                if (!asset && File.Exists(mergedAO))
                    asset = true;

                // --- Build scripts list (AO gated by chapter sanity) ---
                var scripts = new List<string>(3);
                if (sprites) scripts.Add("ImportGraphics.csx");
                if (code)    scripts.Add("ImportGML.csx");
                if (asset)   scripts.Add("ImportAssetOrder.csx");

                if (scripts.Count == 0)
                {
                    Console.WriteLine($"Import complete for chapter {chapter}");
                    continue; // nothing to import
                }

                RunImportScriptsMulti(workingDataWin, scripts.ToArray());
                Console.WriteLine($"Import complete for chapter {chapter}");
            }
        }

        // New helper: run a single UTMT process with multiple --scripts (order preserved).
        public static void RunImportScriptsMulti(string dataWin, string[] scriptNames, bool allowErrors = false)
        {
            using (var proc = new Process())
            {
                if (OperatingSystem.IsWindows())
                {
                    var sb = new StringBuilder();
                    sb.Append($"load \"{dataWin}\" --verbose --output \"{dataWin}\"");
                    foreach (var s in scriptNames)
                        sb.Append($" --scripts \"{Main.pwd}/UTMTCLI/Scripts/{s}\"");
                    proc.StartInfo.FileName = Main.modTool;
                    proc.StartInfo.Arguments = sb.ToString();
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append(Main.modTool + $" load '{dataWin}' --verbose --output '{dataWin}'");
                    foreach (var s in scriptNames)
                        sb.Append($" --scripts '{Main.pwd}/UTMTCLI/Scripts/{s}'");
                    proc.StartInfo.FileName = "/bin/bash";
                    proc.StartInfo.Arguments = "-c \"" + sb.ToString() + "\"";
                }

                proc.StartInfo.CreateNoWindow = false;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                string output = proc.StandardOutput.ReadToEnd();
                string errorOutput = proc.StandardError.ReadToEnd();
                Console.WriteLine(output);

                if (!string.IsNullOrEmpty(errorOutput) && !allowErrors)
                    Console.WriteLine($"Error: {errorOutput}");

                proc.WaitForExit();
            }
        }

        private static bool SanitizeAssetOrderForChapter(int chapter)
        {
            string baseAO   = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "0", "Objects", "AssetOrder.txt");
            string mergedAO = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", "AssetOrder.txt");
            if (!File.Exists(mergedAO) || !File.Exists(baseAO)) return false;

            var baseLines = File.ReadAllLines(baseAO)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim()).ToArray();
            var allowed = new HashSet<string>(baseLines, StringComparer.OrdinalIgnoreCase);

            var lines    = File.ReadAllLines(mergedAO);
            var filtered = lines.Where(l => string.IsNullOrWhiteSpace(l) || allowed.Contains(l.Trim())).ToArray();
            if (filtered.Length != lines.Length)
                Console.WriteLine($"  NOTE: pruned {lines.Length - filtered.Length} AssetOrder entries not present in this chapter.");

            // If filtered order equals vanilla, no need to run AO at all
            bool sameAsBase = filtered.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())
                .SequenceEqual(baseLines, StringComparer.Ordinal);
            if (sameAsBase) return false;

            File.WriteAllLines(mergedAO, filtered);
            return true;
        }


        /// <summary>
        /// Run a script through UndertaleModTool
        /// </summary>
        /// <param name="dataWin"></param>
        /// <param name="scriptName"></param>
        /// <param name="allowErrors"></param>
        public static void RunImportScript(string dataWin, string scriptName, bool allowErrors = false)
        {
            using (var modToolProc = new Process())
            {
                if (OperatingSystem.IsWindows())
                {
                    modToolProc.StartInfo.FileName = Main.modTool;
                    modToolProc.StartInfo.Arguments = $"load \"{dataWin}\" --verbose --output \"{dataWin}\" --scripts \"{Main.pwd}/UTMTCLI/Scripts/{scriptName}\"";
                }
                else
                {
                    modToolProc.StartInfo.FileName = "/bin/bash";
                    modToolProc.StartInfo.Arguments = $"-c \"{Main.modTool} load '{dataWin}' --verbose --output '{dataWin}' --scripts '{Main.pwd}/UTMTCLI/Scripts/{scriptName}'\"";
                }

                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.StartInfo.UseShellExecute = false;
                modToolProc.StartInfo.RedirectStandardOutput = true;
                modToolProc.StartInfo.RedirectStandardError = true;
                modToolProc.Start();

                StreamReader reader = modToolProc.StandardOutput;
                StreamReader errorReader = modToolProc.StandardError;
                string output = reader.ReadToEnd();
                string errorOutput = errorReader.ReadToEnd();

                Console.WriteLine(output);

                if (!string.IsNullOrEmpty(errorOutput) && !allowErrors)
                {
                    Console.WriteLine($"Error: {errorOutput}");
                }

                modToolProc.WaitForExit();
            }
        }
        /// <summary>
        /// Finalises the modpack or set and places a resulting .xdelta and .win in it's own folder
        /// </summary>
        /// <param name="modname"></param>
        public static void result(string modname)
        {
            loadCachedNumbers();

            if (string.IsNullOrWhiteSpace(modname))
                throw new ArgumentException("result(): modname is null/empty");

            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                if (combined)
                {

                    var resChapterDir = Path.Combine(output, "result", modname, chapter.ToString());
                    Directory.CreateDirectory(resChapterDir);


                    using (var proc = new Process())
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            proc.StartInfo.FileName  = Main.DeltaPatcher;
                            proc.StartInfo.Arguments = $"-v -e -f -s \"{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "0", "data.win")}\" " +
                                                       $"\"{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "1", "data.win")}\" " +
                                                       $"\"{Path.Combine(output, "result", modname, $"{modname}-Chapter {chapter}.xdelta")}\"";
                        }
                        else if (OperatingSystem.IsLinux())
                        {
                            proc.StartInfo.FileName  = "/bin/bash";
                            proc.StartInfo.Arguments = $"-c \"{Main.DeltaPatcher} -v -e -f -s '{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "0", "data.win")}' " +
                                                       $"'{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "1", "data.win")}' " +
                                                       $"'{Path.Combine(output, "result", modname, $"{modname}-Chapter {chapter}.xdelta")}'\"";
                        }
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.CreateNoWindow  = true;
                        proc.Start();
                        proc.WaitForExit();
                    }

                    // Copy the combined data.win for the chapter
                    File.Copy(
                        Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "1", "data.win"),
                        Path.Combine(resChapterDir, "data.win"),
                        overwrite: true
                    );

                    // >>> Per-chapter modifiedAssets.txt <<<
                    var srcModListA = Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "1", "modifiedAssets.txt");
                    var srcModListB = Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "modifiedAssets.txt");
                    var dstModList  = Path.Combine(resChapterDir, "modifiedAssets.txt");
                    if (File.Exists(srcModListA)) File.Copy(srcModListA, dstModList, true);
                    else if (File.Exists(srcModListB)) File.Copy(srcModListB, dstModList, true);
                }
                else
                {
                    // One xdelta per mod number under the chapter
                    for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                    {
                        var resModDir = Path.Combine(output, "result", modname, chapter.ToString(), modNumber.ToString());
                        Directory.CreateDirectory(resModDir);

                        using (var proc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                proc.StartInfo.FileName  = Main.DeltaPatcher;
                                proc.StartInfo.Arguments = $"-v -e -f -s \"{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "0", "data.win")}\" " +
                                                           $"\"{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "data.win")}\" " +
                                                           $"\"{Path.Combine(output, "result", modname, chapter.ToString(), $"{modNumber}.xdelta")}\"";
                            }
                            else if (OperatingSystem.IsLinux())
                            {
                                proc.StartInfo.FileName  = "/bin/bash";
                                proc.StartInfo.Arguments = $"-c \"{Main.DeltaPatcher} -v -e -f -s '{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), "0", "data.win")}' " +
                                                           $"'{Path.Combine(output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "data.win")}' " +
                                                           $"'{Path.Combine(output, "result", modname, chapter.ToString(), $"{modNumber}.xdelta")}'\"";
                            }
                            proc.StartInfo.UseShellExecute = false;
                            proc.StartInfo.CreateNoWindow  = true;
                            proc.Start();
                            proc.WaitForExit();
                        }

                        // Copy each mod’s data.win
                        File.Copy(
                            Path.Combine(output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "data.win"),
                            Path.Combine(resModDir, "data.win"),
                            overwrite: true
                        );

                        // Per-mod modifiedAssets.txt
                        var srcModListA = Path.Combine(output, "xDeltaCombiner", chapter.ToString(), modNumber.ToString(), "modifiedAssets.txt");
                        var dstModList  = Path.Combine(resModDir, "modifiedAssets.txt");
                        if (File.Exists(srcModListA))
                            File.Copy(srcModListA, dstModList, overwrite: true);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes folders that are in the GM3P executable folder, by default it deletes /output/xDeltaCombiner and /Cache/running
        /// </summary>
        /// <param name="erase"></param>
        public static void clear(string erase = "runningCache")
        {
            switch (erase)
            {
                case "runningCache":
                case null:
                    if (Directory.Exists(@output + "/xDeltaCombiner/"))
                        Directory.Delete(@output + "/xDeltaCombiner/", true);
                    if (Directory.Exists(@output + "/Cache/running"))
                        Directory.Delete(@output + "/Cache/running", true);
                    break;
                case "cache":
                    if (Directory.Exists(@output + "/Cache/"))
                        Directory.Delete(@output + "/Cache/", true);
                    break;
                case "output":
                    if (Directory.Exists(@output))
                        Directory.Delete(@output, true);
                    if (Directory.Exists(@pwd + "/Packer/"))
                        Directory.Delete(@pwd + "/Packer/", true);
                    break;
                case "uninstall":
                    if (Directory.Exists(@pwd))
                        Directory.Delete(@pwd, true);
                    break;
                case "modpacks":
                    if (Directory.Exists(@output + "/result/"))
                        Directory.Delete(@output + "/result/", true);
                    break;
                default:
                    Console.WriteLine("That's not a valid option");
                    break;
            }
        }

        private static (int width, int height)? TryGetPngSize(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                if (fs.Length < 24) return null;

                Span<byte> sig = stackalloc byte[8];
                if (fs.Read(sig) != 8) return null;
                if (!sig.SequenceEqual(PNG_SIGNATURE)) return null;

                fs.Position = 16; // IHDR width/height
                Span<byte> wh = stackalloc byte[8];
                if (fs.Read(wh) != 8) return null;

                int w = (wh[0] << 24) | (wh[1] << 16) | (wh[2] << 8) | wh[3];
                int h = (wh[4] << 24) | (wh[5] << 16) | (wh[6] << 8) | wh[7];
                return (w, h);
            }
            catch { return null; }
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("libc", SetLastError = true)]
        private static extern int link(string existingFile, string newFile);


        /// <summary>
        /// Errors to print when running load()
        /// </summary>
        public static string loadError { get; set; }
        /// <summary>
        /// load templates
        /// </summary>
        /// <param name="filepath"></param>
        public static void load(string filepath = null)
        {
            if (filepath == null)
            {
                if (File.Exists(Main.pwd + "/template.xrune"))
                {
                    filepath = Main.pwd + "/template.xrune";
                }
            }

            if (filepath != null)
            {
                if (Convert.ToDouble(Main.GetLine(filepath, 1)) >= 0.4)
                {
                    string OpToPerform = Main.GetLine(filepath, 2);
                    modAmount = Convert.ToInt32(Main.GetLine(filepath, 3));
                    vanilla2 = Main.GetLine(filepath, 4);
                    output = Main.GetLine(filepath, 5);
                    DeltaPatcher = Main.GetLine(filepath, 6);
                    modTool = Main.GetLine(filepath, 7);

                    if (OpToPerform == "regular")
                    {
                        CreateCombinerDirectories();
                        CopyVanilla();
                        massPatch(Main.GetLine(filepath, 8).Split(",").ToArray());
                        modifiedListCreate();
                        CompareCombine();
                        result(Main.GetLine(filepath, 9));
                    }
                }
                else
                {
                    loadError = "The template's version is not supported";
                }
            }
            else
            {
                loadError = "The Template doesn't exist";
            }
        }
    }
}

// Logging class
class ConsoleCopy : IDisposable
{
    FileStream fileStream;
    StreamWriter fileWriter;
    TextWriter doubleWriter;
    TextWriter oldOut;

    class DoubleWriter : TextWriter
    {
        TextWriter one;
        TextWriter two;

        public DoubleWriter(TextWriter one, TextWriter two)
        {
            this.one = one;
            this.two = two;
        }

        public override Encoding Encoding
        {
            get { return one.Encoding; }
        }

        public override void Flush()
        {
            one.Flush();
            two.Flush();
        }

        public override void Write(char value)
        {
            one.Write(value);
            two.Write(value);
        }
    }

    public ConsoleCopy(string path)
    {
        oldOut = Console.Out;

        try
        {
            fileStream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            fileWriter = new StreamWriter(fileStream);
            fileWriter.AutoFlush = true;
            doubleWriter = new DoubleWriter(fileWriter, oldOut);
        }
        catch (Exception e)
        {
            Console.WriteLine("Cannot open file for writing");
            Console.WriteLine(e.Message);
            return;
        }
        Console.SetOut(doubleWriter);
    }

    public void Dispose()
    {
        Console.SetOut(oldOut);
        if (fileWriter != null)
        {
            fileWriter.Flush();
            fileWriter.Close();
            fileWriter = null;
        }
        if (fileStream != null)
        {
            fileStream.Close();
            fileStream = null;
        }
    }
}

// Console bool response utility
class UtilsConsole
{
    public static bool Confirm(string title)
    {
        ConsoleKey response;
        do
        {
            Console.Write($"{title} [y/n] ");
            response = Console.ReadKey(false).Key;
            if (response != ConsoleKey.Enter)
            {
                Console.WriteLine();
            }
        } while (response != ConsoleKey.Y && response != ConsoleKey.N);

        return (response == ConsoleKey.Y);
    }
}
