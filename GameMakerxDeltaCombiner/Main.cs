using Codeuctivity.ImageSharpCompare;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GM3P
{
    public class ModFileInfo
    {
        public int ModNumber { get; set; }
        public string FilePath { get; set; }
        public string ModName { get; set; }
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

            var pngFiles = Directory.GetFiles(spritesPath, "*.png", SearchOption.AllDirectories);
            var invalidSprites = new List<string>();

            foreach (var pngFile in pngFiles)
            {
                if (!IsValidPNG(pngFile))
                {
                    invalidSprites.Add(Path.GetFileName(pngFile));
                }
            }

            if (invalidSprites.Count > 0)
            {
                Console.WriteLine($"  WARNING: {invalidSprites.Count} invalid sprites detected after merge:");
                foreach (var sprite in invalidSprites.Take(10))
                {
                    Console.WriteLine($"    - {sprite}");
                }
                if (invalidSprites.Count > 10)
                {
                    Console.WriteLine($"    ... and {invalidSprites.Count - 10} more");
                }
            }
            else
            {
                Console.WriteLine($"  ✓ All {pngFiles.Length} sprites validated successfully");
            }
        }

        /// <summary>
        /// Compare sprites considering they might be part of animation strips
        /// </summary>
        /// <param name="sprite1Path"></param>
        /// <param name="sprite2Path"></param>
        /// <returns></returns>
        private static bool AreSpritesDifferent(string sprite1Path, string sprite2Path)
        {
            try
            {
                // First check if files are valid PNGs
                if (!IsValidPNG(sprite1Path) || !IsValidPNG(sprite2Path))
                {
                    return true; // Consider them different if one is invalid
                }

                // Try image comparison
                try
                {
                    return !ImageSharpCompare.ImagesAreEqual(sprite1Path, sprite2Path);
                }
                catch
                {
                    // If image comparison fails, fall back to binary comparison
                    var info1 = new FileInfo(sprite1Path);
                    var info2 = new FileInfo(sprite2Path);

                    // Quick size check
                    if (info1.Length != info2.Length)
                        return true;

                    // Full binary comparison
                    using (var fs1 = File.OpenRead(sprite1Path))
                    using (var fs2 = File.OpenRead(sprite2Path))
                    {
                        var hash1 = SHA1.Create().ComputeHash(fs1);
                        var hash2 = SHA1.Create().ComputeHash(fs2);
                        return !hash1.SequenceEqual(hash2);
                    }
                }
            }
            catch
            {
                return true; // Consider them different if comparison fails
            }
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
                    .OrderBy(d => d);

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
                    File.Copy(@vanilla[chapter], targetPath, true);
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
                    Console.WriteLine($"Enter patches for Chapter {chapter + 1}:");
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

                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    if (string.IsNullOrWhiteSpace(xDeltaFile[modNumber]))
                        continue;

                    string patchFile = xDeltaFile[modNumber].Trim();

                    if (!File.Exists(patchFile))
                    {
                        Console.WriteLine($"WARNING: Patch file not found: {patchFile}");
                        continue;
                    }

                    if (Path.GetExtension(patchFile) == ".csx")
                    {
                        using (var modToolProc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                modToolProc.StartInfo.FileName = Main.@modTool;
                                modToolProc.StartInfo.Arguments = "load " + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win " +
                                    "--verbose --output " + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" +
                                    " --scripts " + patchFile;
                            }
                            if (OperatingSystem.IsLinux())
                            {
                                modToolProc.StartInfo.FileName = "/bin/bash";
                                modToolProc.StartInfo.Arguments = "-c \"" + Main.@modTool + "load '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win' " +
                                    "--verbose --output '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win'" +
                                    " --scripts " + patchFile + "\"";
                            }
                            modToolProc.StartInfo.CreateNoWindow = false;
                            modToolProc.StartInfo.UseShellExecute = false;
                            modToolProc.StartInfo.RedirectStandardOutput = true;
                            modToolProc.Start();

                            StreamReader reader = modToolProc.StandardOutput;
                            string ProcOutput = reader.ReadToEnd();
                            Console.WriteLine(ProcOutput);
                            modToolProc.WaitForExit();
                        }
                    }
                    else if (Path.GetExtension(patchFile) == ".win")
                    {
                        File.Copy(patchFile, @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win", true);
                    }
                    else
                    {
                        File.WriteAllText(@output + "/Cache/modNumbersCache.txt", Convert.ToString(modNumber));
                        using (var bashProc = new Process())
                        {
                            string sourceFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win";
                            string targetFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win";

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

                        if (File.Exists(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win"))
                        {
                            File.Delete(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win");
                            File.Move(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win",
                                     @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win");
                        }
                    }
                    Console.WriteLine($"Patched: {patchFile}");
                }
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
                    Directory.CreateDirectory(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/CodeEntries/");
                    File.WriteAllText(@output + "/Cache/running/modNumbersCache.txt", Convert.ToString(modNumber));
                    if (modNumber != 1)
                    {
                        using (var modToolProc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                modToolProc.StartInfo.FileName = Main.@modTool;
                                modToolProc.StartInfo.Arguments = "load \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win\" " + "--verbose --output \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + "\" --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\" --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllCode.csx\" --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ExportAssetOrder.csx\"";
                            }
                            if (OperatingSystem.IsLinux())
                            {
                                modToolProc.StartInfo.FileName = "/bin/bash";
                                modToolProc.StartInfo.Arguments = "-c \"" + Main.@modTool + "load '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win' " + "--verbose --output '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + "' --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx' --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAllCode.csx' --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ExportAssetOrder.csx'\"";
                            }
                            modToolProc.StartInfo.CreateNoWindow = false;
                            modToolProc.StartInfo.UseShellExecute = false;
                            modToolProc.StartInfo.RedirectStandardOutput = true;
                            modToolProc.Start();

                            StreamReader reader = modToolProc.StandardOutput;
                            string ProcOutput = reader.ReadToEnd();
                            Console.WriteLine(ProcOutput);
                            modToolProc.WaitForExit();
                        }

                        // Verify key files after dump
                        string codeEntriesPath = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/CodeEntries/";
                        if (Directory.Exists(codeEntriesPath))
                        {
                            // Check for any empty files
                            var emptyFiles = Directory.GetFiles(codeEntriesPath, "*.gml")
                                .Select(f => new FileInfo(f))
                                .Where(fi => fi.Length == 0)
                                .ToList();

                            if (emptyFiles.Count > 0)
                            {
                                Console.WriteLine($"  WARNING: {emptyFiles.Count} empty GML files found in Mod {modNumber}:");
                                foreach (var ef in emptyFiles.Take(5))
                                {
                                    Console.WriteLine($"    - {Path.GetFileName(ef.FullName)}");
                                }
                            }
                        }
                    }
                }
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
                Console.WriteLine($"Processing Chapter {chapter + 1}:");

                string vanillaObjectsPath = Main.@output + "/xDeltaCombiner/" + chapter + "/0/Objects/";
                if (!Directory.Exists(vanillaObjectsPath))
                {
                    Console.WriteLine($"  WARNING: No vanilla Objects folder for chapter {chapter + 1}");
                    continue;
                }

                string[] vanillaFiles = Directory.GetFiles(vanillaObjectsPath, "*", SearchOption.AllDirectories);
                int vanillaFileCount = vanillaFiles.Length;
                Console.WriteLine($"  Found {vanillaFileCount} vanilla files");

                // Create dictionaries for faster lookup
                Dictionary<string, string> vanillaFileDict = new Dictionary<string, string>();
                Dictionary<string, string> vanillaFileRelativePaths = new Dictionary<string, string>();

                foreach (var vanillaFile in vanillaFiles)
                {
                    string fileName = Path.GetFileName(vanillaFile);
                    vanillaFileDict[fileName] = vanillaFile;
                    string relativePath = Path.GetRelativePath(vanillaObjectsPath, vanillaFile);
                    vanillaFileRelativePaths[fileName] = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";
                }

                // Track ALL files and their versions
                Dictionary<string, List<ModFileInfo>> allFileVersions = new Dictionary<string, List<ModFileInfo>>();
                Dictionary<string, string> fileDirectories = new Dictionary<string, string>();
                HashSet<string> allKnownFiles = new HashSet<string>();

                // Add ALL vanilla files
                foreach (var vanillaFile in vanillaFiles)
                {
                    string fileName = Path.GetFileName(vanillaFile);
                    string fileDir = vanillaFileRelativePaths[fileName];

                    allKnownFiles.Add(fileName);

                    if (!allFileVersions.ContainsKey(fileName))
                        allFileVersions[fileName] = new List<ModFileInfo>();

                    allFileVersions[fileName].Add(new ModFileInfo
                    {
                        ModNumber = 0,
                        FilePath = vanillaFile,
                        ModName = "Vanilla"
                    });

                    fileDirectories[fileName] = fileDir;
                }

                // Add files from ALL mods
                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    string modObjectsPath = Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/";

                    if (!Directory.Exists(modObjectsPath))
                        continue;

                    string[] modFiles = Directory.GetFiles(modObjectsPath, "*", SearchOption.AllDirectories);
                    Console.WriteLine($"  Mod {modNumber - 1} has {modFiles.Length} files");

                    foreach (string modFile in modFiles)
                    {
                        string fileName = Path.GetFileName(modFile);
                        string relativePath = Path.GetRelativePath(modObjectsPath, modFile);
                        string fileDir = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";

                        allKnownFiles.Add(fileName);

                        if (!fileDirectories.ContainsKey(fileName))
                            fileDirectories[fileName] = fileDir;

                        if (!allFileVersions.ContainsKey(fileName))
                            allFileVersions[fileName] = new List<ModFileInfo>();

                        allFileVersions[fileName].Add(new ModFileInfo
                        {
                            ModNumber = modNumber,
                            FilePath = modFile,
                            ModName = $"Mod {modNumber - 1}"
                        });
                    }
                }

                Console.WriteLine($"Chapter {chapter + 1}: Found {allKnownFiles.Count} unique files across vanilla and {Main.modAmount} mod(s)");

                // Process EVERY file
                foreach (string fileName in allKnownFiles)
                {
                    // Skip AssetOrder.txt for now
                    if (fileName == "AssetOrder.txt")
                        continue;

                    // Fix naming for global scripts
                    string correctedFileName = fileName;

                    var versions = allFileVersions[fileName];
                    string fileDir = fileDirectories.ContainsKey(fileName) ? fileDirectories[fileName] : "";

                    string targetPath;
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        targetPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", fileDir, correctedFileName);
                    }
                    else
                    {
                        targetPath = Path.Combine(@output, "xDeltaCombiner", chapter.ToString(), "1", "Objects", correctedFileName);
                    }

                    var vanillaVersion = versions.FirstOrDefault(v => v.ModNumber == 0);
                    var modVersions = versions.Where(v => v.ModNumber > 0).ToList();

                    // Determine which mod versions are ACTUALLY different from vanilla
                    var differentModVersions = new List<ModFileInfo>();

                    if (vanillaVersion != null && modVersions.Count > 0)
                    {
                        foreach (var modVersion in modVersions)
                        {
                            bool isDifferent = false;

                            try
                            {
                                if (Path.GetExtension(fileName) == ".png")
                                {
                                    try
                                    {
                                        isDifferent = AreSpritesDifferent(modVersion.FilePath, vanillaVersion.FilePath);
                                    }
                                    catch
                                    {
                                        var vanillaInfo = new FileInfo(vanillaVersion.FilePath);
                                        var modInfo = new FileInfo(modVersion.FilePath);
                                        isDifferent = vanillaInfo.Length != modInfo.Length;
                                    }
                                }
                                else
                                {
                                    using (var fs1 = File.OpenRead(vanillaVersion.FilePath))
                                    using (var fs2 = File.OpenRead(modVersion.FilePath))
                                    {
                                        var hash1 = SHA1.Create().ComputeHash(fs1);
                                        var hash2 = SHA1.Create().ComputeHash(fs2);
                                        isDifferent = !hash1.SequenceEqual(hash2);
                                    }
                                }
                            }
                            catch
                            {
                                isDifferent = true;
                            }

                            if (isDifferent)
                                differentModVersions.Add(modVersion);
                        }
                    }
                    else if (vanillaVersion == null && modVersions.Count > 0)
                    {
                        differentModVersions = modVersions;
                    }

                    // Decide what to do with the file
                    if (differentModVersions.Count == 0 && vanillaVersion != null)
                    {
                        // No mods modify this file - copy ALL code files to preserve vanilla
                        if (Path.GetExtension(fileName) == ".gml" ||
                            Path.GetExtension(fileName) == ".txt" ||
                            fileDir.Contains("CodeEntries"))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                            File.Copy(vanillaVersion.FilePath, targetPath, true);

                            // Verify the copy worked
                            if (File.Exists(targetPath))
                            {
                                var info = new FileInfo(targetPath);
                                if (info.Length == 0)
                                {
                                    Console.WriteLine($"  WARNING: {fileName} was copied but is empty!");
                                    // Try to copy again
                                    File.Copy(vanillaVersion.FilePath, targetPath, true);
                                }
                            }
                        }
                        else if (Path.GetExtension(fileName) == ".png" && vanillaVersion != null)
                        {
                            if (IsValidPNG(vanillaVersion.FilePath))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                                File.Copy(vanillaVersion.FilePath, targetPath, true);
                            }
                        }
                    }
                    else if (differentModVersions.Count == 0 && vanillaVersion == null)
                    {
                        // NEW: Handle case where there's no vanilla version and no mods modify the file
                        Console.WriteLine($"  WARNING: No vanilla version for {fileName}, skipping");
                    }
                    else if (differentModVersions.Count == 1)
                    {
                        // Only one mod modifies this file
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                        // Check source file before copying
                        var sourceInfo = new FileInfo(differentModVersions[0].FilePath);
                        if (sourceInfo.Length == 0)
                        {
                            Console.WriteLine($"  WARNING: Source file is already empty: {fileName} from {differentModVersions[0].ModName}");

                            // Try to find vanilla version as fallback
                            if (vanillaVersion != null)
                            {
                                Console.WriteLine($"    Using vanilla version instead");
                                File.Copy(vanillaVersion.FilePath, targetPath, true);
                            }
                            else
                            {
                                if (Path.GetExtension(fileName) == ".png")
                                {
                                    if (IsValidPNG(differentModVersions[0].FilePath))
                                    {
                                        File.Copy(differentModVersions[0].FilePath, targetPath, true);
                                        Console.WriteLine($"  Sprite: {fileName} <- {differentModVersions[0].ModName} (valid PNG)");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  WARNING: {fileName} from {differentModVersions[0].ModName} is not a valid PNG");
                                        if (vanillaVersion != null && IsValidPNG(vanillaVersion.FilePath))
                                        {
                                            Console.WriteLine($"    Using vanilla version instead");
                                            File.Copy(vanillaVersion.FilePath, targetPath, true);
                                        }
                                        else
                                        {
                                            // Still copy but warn
                                            File.Copy(differentModVersions[0].FilePath, targetPath, true);
                                        }
                                    }
                                }
                                else
                                {
                                    File.Copy(differentModVersions[0].FilePath, targetPath, true);
                                }
                            }
                        }
                        else
                        {
                            File.Copy(differentModVersions[0].FilePath, targetPath, true);

                            if (Path.GetExtension(fileName) == ".gml")
                            {
                                Console.WriteLine($"  Code: {fileName} <- {differentModVersions[0].ModName} ({sourceInfo.Length} bytes)");
                            }
                        }

                        // Verify the copy worked
                        var targetInfo = new FileInfo(targetPath);
                        if (targetInfo.Length == 0)
                        {
                            Console.WriteLine($"    ERROR: File became empty after copy!");
                        }

                        using (var fs = File.OpenRead(targetPath))
                        {
                            string hash = Convert.ToBase64String(SHA1.Create().ComputeHash(fs));
                            Main.modifiedAssets.Add(fileName + "        " + hash);
                        }
                    }
                    else if (differentModVersions.Count > 1)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                        if (Path.GetExtension(fileName) == ".png")
                        {
                            // TEST: Better sprite selection
                            var bestSprite = SelectBestSprite(differentModVersions, vanillaVersion);
                            File.Copy(bestSprite.FilePath, targetPath, true);
                            Console.WriteLine($"  Sprite: {fileName} <- {bestSprite.ModName} (best valid version)");
                        }
                        else if (Path.GetExtension(fileName) == ".ogg" ||
                                 Path.GetExtension(fileName) == ".wav" ||
                                 Path.GetExtension(fileName) == ".mp3")
                        {
                            // Audio files - use the last mod's version
                            var lastMod = differentModVersions.Last();
                            File.Copy(lastMod.FilePath, targetPath, true);
                            Console.WriteLine($"  Audio: {fileName} <- {lastMod.ModName} (can't merge audio)");
                        }
                        else if (Path.GetExtension(fileName) == ".gml" ||
                                 Path.GetExtension(fileName) == ".txt" ||
                                 Path.GetExtension(fileName) == ".json" ||
                                 Path.GetExtension(fileName) == ".ini")
                        {
                            // Text files - use simple concatenation merge for reliability
                            Console.WriteLine($"  Merging: {fileName} ({differentModVersions.Count} mods)");

                            bool mergeSuccess = false;

                            // Try simple concatenation merge first
                            if (vanillaVersion != null)
                            {
                                mergeSuccess = PerformSimpleMerge(
                                    vanillaVersion.FilePath,
                                    differentModVersions,
                                    targetPath
                                );
                            }

                            if (!mergeSuccess)
                            {
                                // If merge failed, at least use the first mod's version
                                Console.WriteLine($"    Merge failed, using {differentModVersions[0].ModName}'s version");
                                File.Copy(differentModVersions[0].FilePath, targetPath, true);
                            }

                            // Verify the result isn't empty
                            if (File.Exists(targetPath))
                            {
                                var info = new FileInfo(targetPath);
                                if (info.Length == 0)
                                {
                                    Console.WriteLine($"    WARNING: Merged file is empty! Using vanilla as fallback");
                                    if (vanillaVersion != null)
                                    {
                                        File.Copy(vanillaVersion.FilePath, targetPath, true);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Unknown file type - use last mod's version
                            var lastMod = differentModVersions.Last();
                            File.Copy(lastMod.FilePath, targetPath, true);
                        }

                        if (File.Exists(targetPath))
                        {
                            using (var fs = File.OpenRead(targetPath))
                            {
                                string hash = Convert.ToBase64String(SHA1.Create().ComputeHash(fs));
                                Main.modifiedAssets.Add(fileName + "        " + hash);
                            }
                        }
                    }
                }

                // Handle AssetOrder.txt separately
                HandleAssetOrderFile(chapter, allFileVersions, vanillaFileDict);
                Console.WriteLine("\nValidating sprites after merge...");
                ValidateSprites(chapter);
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

                    //If it's a full data.win, copy the file
                    else if (Path.GetExtension(xDeltaFile[modNumber]) == ".win")
                    {
                        File.Copy(xDeltaFile[modNumber], @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + " ", true);
                    }
                    //else if (Path.GetExtension(xDeltaFile[modNumber]) == "" || Path.GetExtension(xDeltaFile[modNumber]) == null)
                    //{

                    if (newObjects.Count > 0)
                    {
                        Console.WriteLine($"Mod {modNumber - 1} adds {newObjects.Count} new objects: {string.Join(", ", newObjects)}");
                        string newObjectsFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/NewObjects.txt";
                        File.WriteAllLines(newObjectsFile, newObjects);
                    }
                }
                Console.WriteLine("Chapter complete, if you are using the console app and that wasn't the final chapter, enter in the chapter "+(chapter+1)+" patches");
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
                if (Main.modTool == "skip")
                {
                    Console.WriteLine("Manual import mode...");
                    continue;
                }

                Console.WriteLine($"Processing Chapter {chapter + 1}...");

                string workingDataWin = @output + "/xDeltaCombiner/" + chapter + "/1/data.win";

                // Check which mods add new objects
                var modsWithNewObjects = new Dictionary<int, List<string>>();

                for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
                {
                    string newObjectsFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/NewObjects.txt";
                    if (File.Exists(newObjectsFile))
                    {
                        var lines = File.ReadAllLines(newObjectsFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                        if (lines.Count > 0)
                        {
                            modsWithNewObjects[modNumber] = lines;
                            Console.WriteLine($"  Mod {modNumber - 1} adds {lines.Count} new objects");
                        }
                    }
                }

                // Choose base data.win
                if (modsWithNewObjects.Count == 0)
                {
                    Console.WriteLine("  No new objects, using vanilla as base");
                    File.Copy(@output + "/xDeltaCombiner/" + chapter + "/0/data.win", workingDataWin, true);
                }
                else if (modsWithNewObjects.Count == 1)
                {
                    var modWithObjects = modsWithNewObjects.First();
                    Console.WriteLine($"  Using Mod {modWithObjects.Key - 1}'s data.win as base");
                    File.Copy(@output + "/xDeltaCombiner/" + chapter + "/" + modWithObjects.Key + "/data.win", workingDataWin, true);
                }
                else
                {
                    // Multiple mods add objects - use the one with most objects
                    var bestMod = modsWithNewObjects.OrderByDescending(kvp => kvp.Value.Count).First();
                    Console.WriteLine($"  Using Mod {bestMod.Key - 1}'s data.win (has most objects)");
                    File.Copy(@output + "/xDeltaCombiner/" + chapter + "/" + bestMod.Key + "/data.win", workingDataWin, true);
                }

                // Import graphics
                Console.WriteLine("Importing merged graphics...");
                RunImportScript(workingDataWin, "ImportGraphicsAdvanced.csx");

                // Import code
                Console.WriteLine("Importing merged code...");
                RunImportScript(workingDataWin, "ImportGML.csx");

                // Import AssetOrder
                Console.WriteLine("Importing AssetOrder...");
                RunImportScript(workingDataWin, "ImportAssetOrder.csx", true);

                Console.WriteLine($"Import complete for chapter {chapter + 1}");
            }
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
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                if (modname != null || modname != "")
                {
                    if (combined)
                    {
                        Directory.CreateDirectory(@output + "/result/" + modname + "/" + chapter);
                        using (var bashProc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                bashProc.StartInfo.FileName = Main.@DeltaPatcher;
                                bashProc.StartInfo.Arguments = "-v -e -f -s \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/0/data.win" + "\" \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + "\" \"" + Main.@output + "/result/" + modname + "/" + modname + "-Chapter " + chapter + ".xdelta\"";
                            }
                            if (OperatingSystem.IsLinux())
                            {
                                bashProc.StartInfo.FileName = "/bin/bash";
                                bashProc.StartInfo.Arguments = "-c \"" + @DeltaPatcher + "-v -e -f -s '" + Main.@output + "/xDeltaCombiner/" + chapter + "/0/data.win" + "' '" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + "' '" + Main.@output + "/result/" + modname + "/" + modname + "-Chapter " + chapter + ".xdelta'\"";
                            }
                            bashProc.StartInfo.CreateNoWindow = false;
                            bashProc.Start();
                            bashProc.WaitForExit();
                        }
                        File.Copy(@output + "/xDeltaCombiner/" + chapter + "/1/data.win", @output + "/result/" + modname + "/" + chapter + "/data.win");

                        if (File.Exists(@output + "/xDeltaCombiner/0/1/modifiedAssets.txt"))
                            File.Copy(@output + "/xDeltaCombiner/0/1/modifiedAssets.txt", @output + "/result/" + modname + "/0/modifiedAssets.txt");
                    }
                    else
                    {
                        for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                        {
                            Directory.CreateDirectory(@output + "/result/" + modname + "/" + chapter + "/" + modNumber);
                            using (var bashProc = new Process())
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    bashProc.StartInfo.FileName = Main.@DeltaPatcher;
                                    bashProc.StartInfo.Arguments = "-v -e -f -s \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/0/data.win" + "\" \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + "\" \"" + Main.@output + "/result/" + modname + "/" + chapter + "/" + modNumber + ".xdelta\"";
                                }
                                if (OperatingSystem.IsLinux())
                                {
                                    bashProc.StartInfo.FileName = "bin/bash";
                                    bashProc.StartInfo.Arguments = @DeltaPatcher + "-v -e -f -s \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/vanilla/data.win" + "\" \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + "\" \"" + Main.@output + "/result/" + modname + "/" + chapter + "/" + modNumber + ".xdelta\"";
                                }
                                bashProc.StartInfo.CreateNoWindow = false;
                                bashProc.Start();
                                bashProc.WaitForExit();
                            }
                            File.Copy(@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win", @output + "/result/" + modname + "/" + chapter + "/" + modNumber + "/data.win");
                        }
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