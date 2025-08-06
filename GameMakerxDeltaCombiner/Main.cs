using Codeuctivity.ImageSharpCompare;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
namespace GM3P
{
    internal class Main
    {
        /// <summary>
        /// The path to the vanilla game
        /// </Summary>
        public static string? @vanilla2 { get; set; }
        //public static string vanilla = Main.vanilla2.Replace("\"", "");
        /// <summary>
        /// Current working directory
        /// </summary>
        public static string? @pwd = Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetExecutingAssembly().Location)));
        /// <summary>
        /// Output folder
        /// </summary>
        public static string? output { get; set; }
        /// <summary>
        /// path to an xDelta patcher, e.g. xDelta3 or Deltapatcher
        /// </summary>
        public static string? DeltaPatcher { get; set; }
        /// <summary>
        /// Path to KDiff3, used for merging code files
        /// </summary>
        public static string? KDiff3Path { get; set; }
        /// <summary>
        /// Path to the Meld GUI, used for merging code files
        /// </summary>
        public static string? MeldPath { get; set; }
        /// <summary>
        /// Amount of mods to merge
        /// </summary>
        public static int modAmount { get; set; }
        /// <summary>
        /// Currently unused except as a CLI arg, but this will be used to determine what Game Engine the game is in in a far future release. Use "GM" is for GameMaker
        /// </summary>
        public static string? gameEngine { get; set; }
        /// <summary>
        /// A (probably) temporary bool to tell if compareCombine() has been called
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
        
        // Add this method to locate KDiff3
        public static string FindKDiff3()
        {
            // Check if bundled with GM3P - looking for the exe directly in the GM3P folder
            string bundledPath = Path.Combine(pwd, "kdiff3.exe");
            if (File.Exists(bundledPath))
                return bundledPath;
    
            // Also check in a kdiff3 subfolder
            string bundledSubfolderPath = Path.Combine(pwd, "kdiff3", "kdiff3.exe");
            if (File.Exists(bundledSubfolderPath))
                return bundledSubfolderPath;
    
            // Check common installation paths on Windows
            string[] commonPaths = {
                @"C:\Program Files\KDiff3\kdiff3.exe",
                @"C:\Program Files (x86)\KDiff3\kdiff3.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KDiff3", "kdiff3.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "KDiff3", "kdiff3.exe")
            };
    
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }
    
            // On Linux, check if it's in PATH
            if (OperatingSystem.IsLinux())
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "which";
                    process.StartInfo.Arguments = "kdiff3";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
            
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        return output;
                }
            }
    
            return null;
        }
        /// <summary>
        /// Locate Meld merge tool
        /// </summary>
        public static string FindMeld()
        {
            // Check if bundled with GM3P - looking for the exe directly in the GM3P folder
            string bundledPath = Path.Combine(pwd, "meld.exe");
            if (File.Exists(bundledPath))
                return bundledPath;

            // Also check in a meld subfolder
            string bundledSubfolderPath = Path.Combine(pwd, "meld", "meld.exe");
            if (File.Exists(bundledSubfolderPath))
                return bundledSubfolderPath;

            // Check common installation paths on Windows
            string[] commonPaths = {
                @"C:\Program Files\Meld\meld.exe",
                @"C:\Program Files (x86)\Meld\meld.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Meld", "meld.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Meld", "meld.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Meld", "meld.exe")
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // On Linux, check if it's in PATH
            if (OperatingSystem.IsLinux())
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "which";
                    process.StartInfo.Arguments = "meld";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
            
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        return output;
                }
            }

            return null;
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
        
        public static void CheckMeldAvailability()
        {
            string meldPath = FindMeld();
    
            if (string.IsNullOrEmpty(meldPath))
            {
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine("⚠  WARNING: Meld not found!");
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("Meld is REQUIRED for proper merging of multiple mods, especially");
                Console.WriteLine("sprite mods that modify AssetOrder.txt");
                Console.WriteLine();
                Console.WriteLine("Without Meld:");
                Console.WriteLine("  • Code conflicts will overwrite each other");
                Console.WriteLine("  • Sprite ordering will be incorrect");
                Console.WriteLine("  • Only the last mod's changes will be preserved");
                Console.WriteLine();
                Console.WriteLine("Please install Meld from: https://meldmerge.org/");
                Console.WriteLine("Or place meld.exe in the GM3P folder");
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine();
        
                if (!UtilsConsole.Confirm("Continue without Meld? (NOT recommended)"))
                {
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine($"✓ Meld found at: {meldPath}");
            }
        }
        
        /// <summary>
        /// Copy vanilla files as much as needed
        /// </summary>
        public static void CopyVanilla()
        {
            string[] vanilla = [""];
            if (Path.GetExtension(@vanilla2) != ".win")
            {
                vanilla = Directory.GetFiles(Main.@vanilla2, "*.win", SearchOption.AllDirectories);
            }
            else
            {
                vanilla[0] = vanilla2;
            }

                chapterAmount = vanilla.Length;
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
                    File.Copy(@vanilla[chapter], @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win", true);
                }
            }
        }
        
        
        
        public static string[] xDeltaFile { get; set; }
        /// <summary>
        /// The titular function; patches a bunch of mods into data.win files
        /// </summary>
        public static void massPatch(string[] filepath = null)
        {
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                
                xDeltaFile = new string[(modAmount + 2)];

                if (filepath == null)
                {
                    for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                    {
                        xDeltaFile[modNumber] = Console.ReadLine().Replace("\"", "");

                    }
                }
                else
                {
                    string? chapterMods = Convert.ToString(filepath[chapter]);
                    xDeltaFile = chapterMods.Split(",");
                    //for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                    //{
                    //    xDeltaFile[modNumber] = filepath[modNumber].Replace("\"", "");

                    //}
                }

                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    //Check if the mod is a UTMT script. If so, patch it.
                    if (Path.GetExtension(xDeltaFile[modNumber]) == ".csx")
                    {
                        using (var modToolProc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                modToolProc.StartInfo.FileName = Main.@modTool;
                                modToolProc.StartInfo.Arguments = "load " + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win " + "--verbose --output " + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + " --scripts " + xDeltaFile[modNumber];
                            }
                            if (OperatingSystem.IsLinux())
                            {
                                modToolProc.StartInfo.FileName = "/bin/bash";
                                modToolProc.StartInfo.Arguments = "-c \"" + Main.@modTool + "load '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win' " + "--verbose --output '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win'" + " --scripts " + xDeltaFile[modNumber] + "\"";
                            }
                            modToolProc.StartInfo.CreateNoWindow = false;
                            modToolProc.StartInfo.UseShellExecute = false;
                            modToolProc.StartInfo.RedirectStandardOutput = true;
                            modToolProc.Start();
                            // Synchronously read the standard output of the spawned process.
                            StreamReader reader = modToolProc.StandardOutput;
                            string ProcOutput = reader.ReadToEnd();

                            // Write the redirected output to this application's window.
                            Console.WriteLine(ProcOutput);

                            modToolProc.WaitForExit();
                        }
                    }
                    //If it's a full data.win, copy the file
                    else if (Path.GetExtension(xDeltaFile[modNumber]) == ".win")
                    {
                        File.Copy(xDeltaFile[modNumber], @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win" + " ", true);
                    }
                    //else if (Path.GetExtension(xDeltaFile[modNumber]) == "" || Path.GetExtension(xDeltaFile[modNumber]) == null)
                    //{

                    //}
                    //Otherwise, patch the xDelta
                    else
                    {
                        File.WriteAllText(@output + "/Cache/modNumbersCache.txt", Convert.ToString(modNumber));
                        using (var bashProc = new Process())
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                bashProc.StartInfo.FileName = Main.@DeltaPatcher;
                                bashProc.StartInfo.Arguments = "-v -d -f -s \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win\"" + " \"" + xDeltaFile[modNumber] + "\" \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win" + "\" ";
                            }
                            if (OperatingSystem.IsLinux())
                            {
                                bashProc.StartInfo.FileName = "/bin/bash";
                                bashProc.StartInfo.Arguments = "-c \"" + @DeltaPatcher + "-v -d -f -s '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win'" + " '" + xDeltaFile[modNumber] + "' '" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win" + "'\" ";
                            }
                            bashProc.StartInfo.CreateNoWindow = false;
                            bashProc.StartInfo.UseShellExecute = false;
                            bashProc.StartInfo.RedirectStandardOutput = true;
                            bashProc.Start();
                            // Synchronously read the standard output of the spawned process.
                            StreamReader reader = bashProc.StandardOutput;
                            string ProcOutput = reader.ReadToEnd();

                            // Write the redirected output to this application's window.
                            Console.WriteLine(ProcOutput);

                            bashProc.WaitForExit();
                        }
                        if (File.Exists("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win"))
                        {
                            File.Delete("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win");
                            File.Move("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/dat.win", "" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/data.win");
                        }
                    }
                    Console.WriteLine("Patched: " + xDeltaFile[modNumber]);
                }
                Console.WriteLine("Chapter complete, if you are using the console app and that wasn't the final chapter, enter in the chapter "+(chapter+1)+" patches");
            }
            Console.WriteLine("\nMass Patch complete, continue or use the compare command to combine mods");
        }

        public static List<string> modifedAssets = new List<string> { "Asset Name                       Hash (SHA1 in Base64)" };
        public static void modifiedListCreate()
        {
            loadCachedNumbers();
            for (int i = 0; i < chapterAmount; i++)
            {
                if (!File.Exists(@output + "/xDeltaCombiner/" + i + "/1/modifedAssets.txt"))
                {
                    File.Create(@output + "/xDeltaCombiner/" + i + "/1/modifedAssets.txt").Close();
                }
            }

        }
        /// <summary>
        /// Dumps objects from mod
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

                            // Synchronously read the standard output of the spawned process.
                            StreamReader reader = modToolProc.StandardOutput;
                            string ProcOutput = reader.ReadToEnd();

                            // Write the redirected output to this application's window.
                            Console.WriteLine(ProcOutput);

                            modToolProc.WaitForExit();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// The function that's the main draw of GM3P; Compares and Combines objects
        /// </summary>
        public static void CompareCombine()
        {
            loadCachedNumbers();
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                string[] vanillaFiles = Directory.GetFiles("" + Main.@output + "/xDeltaCombiner/"+chapter+"/0/Objects/", "*", SearchOption.AllDirectories);
                string[] vanillaFilesName = vanillaFiles.Select(Path.GetFileName).ToArray();
                int vanillaFileCount = Convert.ToInt32(vanillaFiles.Length);
                
                // Create a dictionary for faster vanilla file lookup
                Dictionary<string, string> vanillaFileDict = new Dictionary<string, string>();
                for (int i = 0; i < vanillaFiles.Length; i++)
                {
                    vanillaFileDict[Path.GetFileName(vanillaFiles[i])] = vanillaFiles[i];
                }
                
                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    int modFileCount = Convert.ToInt32(Directory.GetFiles("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/", "*", SearchOption.AllDirectories).Length);
                    string[] modFiles = Directory.GetFiles("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/", "*", SearchOption.AllDirectories);
                    string[] modFilesName = modFiles.Select(Path.GetFileName).ToArray();
                    string[] modFileAdditions = modFilesName.Except(vanillaFilesName).ToArray();
                    int modFileAdditionsCount = Convert.ToInt32(modFileAdditions.Length);
                    
                    // Process each mod file
                    for (int i = 0; i < modFileCount; i++)
                    {
                        string modFileName = Path.GetFileName(modFiles[i]);
                        string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFiles[i]))?.Name + "/" + Directory.GetParent(modFiles[i])?.Name;
                        
                        // Skip AssetOrder.txt for now (handled separately)
                        if (modFileName == "AssetOrder.txt")
                            continue;
                        
                        // Check if this file exists in vanilla
                        if (vanillaFileDict.ContainsKey(modFileName))
                        {
                            string vanillaFile = vanillaFileDict[modFileName];
                            Console.WriteLine("Currently Comparing " + modFileName + " (vanilla vs mod " + (modNumber - 1) + ")");
                            
                            // Compare the files
                            SHA1 vanillaHashing = new SHA1CryptoServiceProvider();
                            using (FileStream fs = File.OpenRead(vanillaFile))
                            {
                                string vanillaHash = Convert.ToBase64String(vanillaHashing.ComputeHash(fs));
                                SHA1 modHashing = new SHA1CryptoServiceProvider();
                                
                                using (FileStream fx = File.OpenRead(modFiles[i]))  // Use the correct mod file
                                {
                                    string modHash = Convert.ToBase64String(modHashing.ComputeHash(fx));
                                    Console.WriteLine(modHash);
                                    
                                    if (modHash != vanillaHash)
                                    {
                                        Console.WriteLine(vanillaHash);
                                        Console.WriteLine(modFileDir);
                                        
                                        if (modFileDir == ("Objects/CodeEntries"))
                                        {
                                            string targetPath = @output + "/xDeltaCombiner/" + chapter + "/1/Objects/CodeEntries/" + modFileName;
                                            
                                            if (File.Exists(targetPath))
                                            {
                                                Console.WriteLine($"Conflict detected: {modFileName} modified by multiple mods");
                                                
                                                // Try to find Meld
                                                if (string.IsNullOrEmpty(MeldPath))
                                                {
                                                    MeldPath = FindMeld();
                                                    if (!string.IsNullOrEmpty(MeldPath))
                                                    {
                                                        Console.WriteLine($"Found Meld at: {MeldPath}");
                                                    }
                                                }
                                                
                                                if (!string.IsNullOrEmpty(MeldPath))
                                                {
                                                    Console.WriteLine("Attempting 3-way merge using Meld...");
                                                    
                                                    string vanillaCodeFile = vanillaFile;
                                                    string currentMerged = targetPath;
                                                    string newModFile = modFiles[i];
                                                    string outputFile = currentMerged; // Overwrite the current merged file
                                                    
                                                    using (var meldProc = new Process())
                                                    {
                                                        if (OperatingSystem.IsWindows())
                                                        {
                                                            meldProc.StartInfo.FileName = MeldPath;
                                                            // Meld arguments: --auto-merge mine base theirs --output result
                                                            meldProc.StartInfo.Arguments = $"--auto-merge \"{currentMerged}\" \"{vanillaCodeFile}\" \"{newModFile}\" --output=\"{outputFile}\"";
                                                        }
                                                        else if (OperatingSystem.IsLinux())
                                                        {
                                                            meldProc.StartInfo.FileName = MeldPath;
                                                            meldProc.StartInfo.Arguments = $"--auto-merge '{currentMerged}' '{vanillaCodeFile}' '{newModFile}' --output='{outputFile}'";
                                                        }
                                                        
                                                        meldProc.StartInfo.CreateNoWindow = true;
                                                        meldProc.StartInfo.UseShellExecute = false;
                                                        meldProc.StartInfo.RedirectStandardOutput = true;
                                                        meldProc.StartInfo.RedirectStandardError = true;
                                                        
                                                        meldProc.Start();
                                                        string output = meldProc.StandardOutput.ReadToEnd();
                                                        string errors = meldProc.StandardError.ReadToEnd();
                                                        meldProc.WaitForExit();
                                                        
                                                        if (meldProc.ExitCode == 0 || meldProc.ExitCode == 1)
                                                        {
                                                            Console.WriteLine("Successfully merged changes from both mods");
                                                            
                                                            // Check if file has conflict markers
                                                            if (File.Exists(outputFile))
                                                            {
                                                                string mergedContent = File.ReadAllText(outputFile);
                                                                if (mergedContent.Contains("<<<<<<<") || mergedContent.Contains(">>>>>>>"))
                                                                {
                                                                    Console.WriteLine("Warning: Unresolved conflicts may remain in the file");
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine($"Meld merge failed with exit code {meldProc.ExitCode}");
                                                            if (!string.IsNullOrEmpty(errors))
                                                                Console.WriteLine($"Error: {errors}");
                                                            
                                                            // Fall back to simple overwrite
                                                            Console.WriteLine("Falling back to overwrite mode - keeping changes from latest mod");
                                                            File.Copy(newModFile, outputFile, true);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Meld not found - cannot merge code changes");
                                                    Console.WriteLine("WARNING: Overwriting with latest mod's changes. Previous mod's code changes will be lost!");
                                                    Console.WriteLine("To enable automatic merging, install Meld from https://meldmerge.org/");
                                                    
                                                    // Simple overwrite
                                                    File.Copy(modFiles[i], targetPath, true);
                                                }
                                            }
                                            else
                                            {
                                                // First mod to modify this file - just copy it
                                                File.Copy(modFiles[i], targetPath, true);
                                            }
                                            Console.WriteLine("Copying " + Path.GetFileName(modFiles[i]));
                                            Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                        }
                                        else if (modFileDir != ("Objects/CodeEntries"))
                                        {
                                            if (Path.GetExtension(modFiles[i]) == ".png")
                                            {
                                                try
                                                {
                                                    bool compareMemCmp = ImageSharpCompare.ImagesAreEqual(modFiles[i], vanillaFile);

                                                    if (!compareMemCmp)
                                                    {
                                                        Directory.CreateDirectory(@output + "/xDeltaCombiner/" + chapter + "/1/Objects/" + modFileDir);
                                                        File.Copy(modFiles[i], @output + "/xDeltaCombiner/" + chapter + "/1/Objects/" + modFileDir + "/" + modFileName, true);
                                                        Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    StackTrace rew = new StackTrace();
                                                    Console.WriteLine(rew);
                                                    Console.WriteLine(ex.Message);
                                                    Console.WriteLine(modFiles[i]);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Handle new files (additions)
                    if (modFileAdditionsCount >= 1)
                    {
                        string[] modFileAddtionsDir = Directory.GetFiles("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/", "*", SearchOption.AllDirectories).Select(Path.GetFullPath).ToArray();
                        for (int i = 0; i < modFileAdditionsCount; i++)
                        {
                            string[] modFilesParent = Directory.GetFiles("" + Main.@output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/", modFileAdditions[i], SearchOption.AllDirectories);
                            string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFilesParent[0])).Name + "/" + Directory.GetParent(modFilesParent[0]).Name;

                            if (Path.GetExtension(modFileAdditions[i]) == ".gml")
                            {
                                File.Copy(modFilesParent[0], @output + "/xDeltaCombiner/" + chapter + "/1/Objects/CodeEntries/" + Path.GetFileName(modFileAdditions[i]), true);
                                modifedAssets.Add(Path.GetFileName(modFilesParent[0]) + "        ");
                            }
                            if (Path.GetExtension(modFileAdditions[i]) == ".png")
                            {
                                Directory.CreateDirectory(@output + "/xDeltaCombiner/" + chapter + "/1/Objects/" + modFileDir);
                                File.Copy(modFilesParent[0], @output + "/xDeltaCombiner/" + chapter + "/1/Objects/" + modFileDir + "/" + Path.GetFileName(modFileAdditions[i]), true);
                                modifedAssets.Add(Path.GetFileName(modFilesParent[0]) + "        ");
                            }
                            Console.WriteLine("Currently Copying " + Path.GetFileName(modFileAdditions[i]));
                            Console.WriteLine(modFileAdditionsCount);
                        }
                    }
                    
                    // Handle AssetOrder.txt merging with Meld
                    string assetOrderFile = @output + "/xDeltaCombiner/" + chapter + "/1/Objects/AssetOrder.txt";
                    string vanillaAssetOrder = @output + "/xDeltaCombiner/" + chapter + "/0/Objects/AssetOrder.txt";
                    string modAssetOrder = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/AssetOrder.txt";

                    if (File.Exists(modAssetOrder))
                    {
                        if (File.Exists(assetOrderFile))
                        {
                            // AssetOrder.txt exists from previous mod - need to merge
                            Console.WriteLine("Merging AssetOrder.txt files...");
                            
                            // Try to find Meld if not already found
                            if (string.IsNullOrEmpty(MeldPath))
                            {
                                MeldPath = FindMeld();
                            }
                            
                            if (!string.IsNullOrEmpty(MeldPath))
                            {
                                using (var meldProc = new Process())
                                {
                                    if (OperatingSystem.IsWindows())
                                    {
                                        meldProc.StartInfo.FileName = MeldPath;
                                        meldProc.StartInfo.Arguments = $"--auto-merge \"{assetOrderFile}\" \"{vanillaAssetOrder}\" \"{modAssetOrder}\" --output=\"{assetOrderFile}\"";
                                    }
                                    else if (OperatingSystem.IsLinux())
                                    {
                                        meldProc.StartInfo.FileName = MeldPath;
                                        meldProc.StartInfo.Arguments = $"--auto-merge '{assetOrderFile}' '{vanillaAssetOrder}' '{modAssetOrder}' --output='{assetOrderFile}'";
                                    }
                                    
                                    meldProc.StartInfo.CreateNoWindow = true;
                                    meldProc.StartInfo.UseShellExecute = false;
                                    meldProc.StartInfo.RedirectStandardOutput = true;
                                    meldProc.StartInfo.RedirectStandardError = true;
                                    
                                    meldProc.Start();
                                    meldProc.WaitForExit();
                                    
                                    if (meldProc.ExitCode == 0 || meldProc.ExitCode == 1)
                                    {
                                        Console.WriteLine("Successfully merged AssetOrder.txt");
                                    }
                                    else
                                    {
                                        Console.WriteLine("AssetOrder.txt merge had issues - sprite ordering may be affected");
                                    }
                                }
                            }
                            else
                            {
                                // No Meld - critical for sprite mods
                                Console.WriteLine("WARNING: Cannot merge AssetOrder.txt without Meld!");
                                Console.WriteLine("Sprite ordering will be incorrect. Install Meld from https://meldmerge.org/");
                                
                                // Simple append approach as fallback
                                var existingLines = File.ReadAllLines(assetOrderFile).ToList();
                                var modLines = File.ReadAllLines(modAssetOrder).ToList();
                                var vanillaLines = File.ReadAllLines(vanillaAssetOrder).ToList();
                                
                                // Find new entries in mod that aren't in vanilla or existing
                                var newEntries = modLines.Except(vanillaLines).Except(existingLines).ToList();
                                
                                if (newEntries.Count > 0)
                                {
                                    Console.WriteLine($"Adding {newEntries.Count} new asset entries (order may be wrong)");
                                    existingLines.AddRange(newEntries);
                                    File.WriteAllLines(assetOrderFile, existingLines);
                                }
                            }
                        }
                        else
                        {
                            // First mod's AssetOrder - just copy it
                            File.Copy(modAssetOrder, assetOrderFile, true);
                        }
                    }

                    // Also ensure we have the vanilla AssetOrder as a base
                    if (!File.Exists(assetOrderFile) && File.Exists(vanillaAssetOrder))
                    {
                        File.Copy(vanillaAssetOrder, assetOrderFile, true);
                    }
                }
            }
            Main.combined = true;
        }
        
        /// <summary>
        /// Detects and handles new objects added by mods
        /// </summary>
        public static void HandleNewObjects()
        {
            loadCachedNumbers();
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                Console.WriteLine($"Checking for new objects in chapter {chapter}...");
                
                // Get list of objects from vanilla
                HashSet<string> vanillaObjects = new HashSet<string>();
                string vanillaCodePath = @output + "/xDeltaCombiner/" + chapter + "/0/Objects/CodeEntries/";
                if (Directory.Exists(vanillaCodePath))
                {
                    foreach (string file in Directory.GetFiles(vanillaCodePath, "gml_Object_*"))
                    {
                        // Extract object name from filename like "gml_Object_obj_name_Create_0.gml"
                        string filename = Path.GetFileName(file);
                        if (filename.StartsWith("gml_Object_"))
                        {
                            string[] parts = filename.Split('_');
                            if (parts.Length >= 3)
                            {
                                // Get the object name (everything between "gml_Object_" and the event type)
                                int startIndex = "gml_Object_".Length;
                                int endIndex = filename.LastIndexOf("_Create_") != -1 ? filename.LastIndexOf("_Create_") :
                                              filename.LastIndexOf("_Step_") != -1 ? filename.LastIndexOf("_Step_") :
                                              filename.LastIndexOf("_Draw_") != -1 ? filename.LastIndexOf("_Draw_") :
                                              filename.LastIndexOf("_Alarm_") != -1 ? filename.LastIndexOf("_Alarm_") :
                                              filename.LastIndexOf("_Destroy_") != -1 ? filename.LastIndexOf("_Destroy_") :
                                              filename.LastIndexOf("_Collision_") != -1 ? filename.LastIndexOf("_Collision_") :
                                              filename.LastIndexOf("_Other_") != -1 ? filename.LastIndexOf("_Other_") :
                                              filename.LastIndexOf("_PreCreate_") != -1 ? filename.LastIndexOf("_PreCreate_") : -1;
                                
                                if (endIndex > startIndex)
                                {
                                    string objectName = filename.Substring(startIndex, endIndex - startIndex);
                                    vanillaObjects.Add(objectName);
                                }
                            }
                        }
                    }
                }
                
                // Check each mod for new objects
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
                                int startIndex = "gml_Object_".Length;
                                int endIndex = filename.LastIndexOf("_Create_") != -1 ? filename.LastIndexOf("_Create_") :
                                              filename.LastIndexOf("_Step_") != -1 ? filename.LastIndexOf("_Step_") :
                                              filename.LastIndexOf("_Draw_") != -1 ? filename.LastIndexOf("_Draw_") :
                                              filename.LastIndexOf("_Alarm_") != -1 ? filename.LastIndexOf("_Alarm_") :
                                              filename.LastIndexOf("_Destroy_") != -1 ? filename.LastIndexOf("_Destroy_") :
                                              filename.LastIndexOf("_Collision_") != -1 ? filename.LastIndexOf("_Collision_") :
                                              filename.LastIndexOf("_Other_") != -1 ? filename.LastIndexOf("_Other_") :
                                              filename.LastIndexOf("_PreCreate_") != -1 ? filename.LastIndexOf("_PreCreate_") : -1;
                                
                                if (endIndex > startIndex)
                                {
                                    string objectName = filename.Substring(startIndex, endIndex - startIndex);
                                    if (!vanillaObjects.Contains(objectName))
                                    {
                                        newObjects.Add(objectName);
                                    }
                                }
                            }
                        }
                    }
                    
                    if (newObjects.Count > 0)
                    {
                        Console.WriteLine($"Mod {modNumber - 1} adds {newObjects.Count} new objects: {string.Join(", ", newObjects)}");
                        
                        // Create a file listing new objects for the import script to use
                        string newObjectsFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/NewObjects.txt";
                        File.WriteAllLines(newObjectsFile, newObjects);
                    }
                }
            }
        }
        
        /// <summary>
        /// Imports resulting GameMaker Objects from the "CompareCombine()" function into a data.win
        /// </summary>
        public static void importWithNewObjects()
        {
            loadCachedNumbers();
            for (int chapter = 0; chapter < chapterAmount; chapter++)
            {
                if (Main.modTool == "skip")
                {
                    Console.WriteLine("In order to replace and import manually, load up the data.win in /xDeltaCombiner/*chapter*/1/ into the GUI version of UTMT and run the script ImportGML.csx. Select \"C:/xDeltaCombiner/*currentsubfolder*/Objects/\" as the import folder. Once finished, exit and saving.");
                }
                else
                {
                    // First, we need to apply any mods that add new objects to get their object definitions
                    List<int> modsWithNewObjects = new List<int>();
                    for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
                    {
                        string newObjectsFile = @output + "/xDeltaCombiner/" + chapter + "/" + modNumber + "/Objects/NewObjects.txt";
                        if (File.Exists(newObjectsFile))
                        {
                            modsWithNewObjects.Add(modNumber);
                        }
                    }
                    
                    // If any mods add new objects, we need to apply them first to create the object definitions
                    if (modsWithNewObjects.Count > 0)
                    {
                        Console.WriteLine($"Found {modsWithNewObjects.Count} mods that add new objects. Creating object definitions...");
                        
                        // Copy vanilla data.win to working file
                        File.Copy(@output + "/xDeltaCombiner/" + chapter + "/0/data.win", 
                                  @output + "/xDeltaCombiner/" + chapter + "/1/data.win", true);
                        
                        // Apply each mod that adds new objects using UTMT scripts
                        foreach (int modNumber in modsWithNewObjects)
                        {
                            Console.WriteLine($"Creating object definitions from mod {modNumber - 1}...");
                            
                            // We need a special script that creates the new objects
                            // For now, we'll apply the full mod to get the object definitions
                            using (var bashProc = new Process())
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    bashProc.StartInfo.FileName = Main.@DeltaPatcher;
                                    bashProc.StartInfo.Arguments = "-v -d -f -s \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win\"" + 
                                                                 " \"" + Main.xDeltaFile[modNumber] + "\" \"" + 
                                                                 Main.@output + "/xDeltaCombiner/" + chapter + "/1/data_temp.win" + "\" ";
                                }
                                if (OperatingSystem.IsLinux())
                                {
                                    bashProc.StartInfo.FileName = "/bin/bash";
                                    bashProc.StartInfo.Arguments = "-c \"" + @DeltaPatcher + "-v -d -f -s '" + 
                                                                 Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win'" + 
                                                                 " '" + Main.xDeltaFile[modNumber] + "' '" + 
                                                                 Main.@output + "/xDeltaCombiner/" + chapter + "/1/data_temp.win" + "'\" ";
                                }
                                bashProc.StartInfo.CreateNoWindow = false;
                                bashProc.StartInfo.UseShellExecute = false;
                                bashProc.StartInfo.RedirectStandardOutput = true;
                                bashProc.Start();
                                bashProc.WaitForExit();
                                
                                if (File.Exists(Main.@output + "/xDeltaCombiner/" + chapter + "/1/data_temp.win"))
                                {
                                    File.Delete(Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win");
                                    File.Move(Main.@output + "/xDeltaCombiner/" + chapter + "/1/data_temp.win", 
                                              Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win");
                                }
                            }
                        }
                        
                        Console.WriteLine("Object definitions created. Now importing code and assets...");
                    }
                    
                    // Now proceed with normal import
                    using (var modToolProc = new Process())
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            modToolProc.StartInfo.FileName = Main.@modTool;
                            modToolProc.StartInfo.Arguments = "load \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win\" " + 
                                                            "--verbose --output \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + 
                                                            "\" --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ImportGraphicsAdvanced.csx\" --scripts \"" + 
                                                            Main.@pwd + "/UTMTCLI/Scripts/ImportGML.csx\"";
                        }
                        if (OperatingSystem.IsLinux())
                        {
                            modToolProc.StartInfo.FileName = "/bin/bash";
                            modToolProc.StartInfo.Arguments = "-c \"" + @modTool + "load '" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win' " + 
                                                             "--verbose --output '" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + 
                                                             "' --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ImportGraphicsAdvanced.csx' --scripts '" + 
                                                             Main.@pwd + "/UTMTCLI/Scripts/ImportGML.csx'\"";
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
                    
                    // Import AssetOrder
                    using (var modToolProc = new Process())
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            modToolProc.StartInfo.FileName = Main.@modTool;
                            modToolProc.StartInfo.Arguments = "load \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win\" " + 
                                                            "--verbose --output \"" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + 
                                                            "\" --scripts \"" + Main.@pwd + "/UTMTCLI/Scripts/ImportAssetOrder.csx\"";
                        }
                        if (OperatingSystem.IsLinux())
                        {
                            modToolProc.StartInfo.FileName = "/bin/bash";
                            modToolProc.StartInfo.Arguments = "-c \"" + @modTool + "load '" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win' " + 
                                                             "--verbose --output '" + Main.@output + "/xDeltaCombiner/" + chapter + "/1/data.win" + 
                                                             "' --scripts '" + Main.@pwd + "/UTMTCLI/Scripts/ImportAssetOrder.csx'\"";
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
            }
        }
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
                        File.Copy(@output + "/xDeltaCombiner/0/1/modifedAssets.txt", @output + "/result/" + modname + "/0/modifedAssets.txt");
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
        public static void clear(string erase = "runningCache")
        {
            switch (erase)
            {
                case "runningCache":
                    Directory.Delete(@output + "/xDeltaCombiner/", true);
                    Directory.Delete(@output + "/Cache/running", true);
                    break;

                case null:
                    Directory.Delete(@output + "/xDeltaCombiner/", true);
                    Directory.Delete(@output + "/Cache/running", true);
                    break;

                case "cache":
                    Directory.Delete(@output + "/Cache/", true);
                    break;

                case "output":
                    Directory.Delete(@output, true);
                    Directory.Delete(@pwd + "/Packer/", true);
                    break;
                case "uninstall":
                    Directory.Delete(@pwd, true);
                    break;
                case "modpacks":
                    Directory.Delete(@output + "/result/", true);
                    break;

                default:
                    Console.WriteLine("That's not a vaild option");
                    break;
            }
        }
        /// <summary>
        /// Error to return if load() fails
        /// </summary>
        public static string loadError { get; set; }
        /// <summary>
        /// Loads Template
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
                if (Main.GetLine(filepath, 1) == "0.4")
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
            if (filepath == null)
            {
                loadError = "The Template doesn't exists";
            }

        }
    }
}
//A logging class C+Ped from https://stackoverflow.com/questions/420429/mirroring-console-output-to-a-file
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

//A console bool response thingy from https://stackoverflow.com/a/54127216
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

