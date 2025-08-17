using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GM3P.Data;

namespace GM3P.Merging
{
    public interface IGitService
    {
        string? FindGit();
        bool PerformGitMerge(string baseFile, List<ModFileInfo> mods, string outputFile);
        string? RunGitCommand(string gitPath, string workingDir, string arguments, bool allowNonZeroExit = false);
    }

    public class GitService : IGitService
    {
        private readonly string _pwd;

        public GitService(string workingDirectory)
        {
            _pwd = workingDirectory;
        }

        public string? FindGit()
        {
            try
            {
                // Check for portable Git in GM3P folder FIRST
                string[] portableGitPaths = {
                    Path.Combine(_pwd, "git", "cmd", "git.exe"),
                    Path.Combine(_pwd, "git", "bin", "git.exe"),
                    Path.Combine(_pwd, "git", "mingw64", "bin", "git.exe")
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

                    string? output = process.StandardOutput.ReadLine();
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

        public bool PerformGitMerge(string baseFile, List<ModFileInfo> mods, string outputFile)
        {
            try
            {
                string? gitPath = FindGit();
                if (string.IsNullOrEmpty(gitPath))
                {
                    Console.WriteLine("    Git not available, using fallback merge");
                    return PerformSimpleMerge(baseFile, mods, outputFile);
                }

                Console.WriteLine($"    Using git merge with {mods.Count} mod(s)");

                string tempRepo = Path.Combine(Path.GetTempPath(), $"GM3P_{Guid.NewGuid():N}".Substring(0, 8));
                Directory.CreateDirectory(tempRepo);

                try
                {
                    string workFile = Path.Combine(tempRepo, "file.txt");

                    // Initialize git repo
                    var initResult = RunGitCommand(gitPath, tempRepo, "init -q");
                    if (initResult == null)
                    {
                        Console.WriteLine("    Git init failed");
                        return PerformSimpleMerge(baseFile, mods, outputFile);
                    }

                    RunGitCommand(gitPath, tempRepo, "config user.email \"gm3p@local\"");
                    RunGitCommand(gitPath, tempRepo, "config user.name \"GM3P\"");
                    RunGitCommand(gitPath, tempRepo, "config core.autocrlf false");
                    RunGitCommand(gitPath, tempRepo, "config merge.ours.driver \"true\"");

                    // Create base commit
                    File.Copy(baseFile, workFile, true);
                    RunGitCommand(gitPath, tempRepo, "add .");
                    var baseCommit = RunGitCommand(gitPath, tempRepo, "commit -q -m \"base\" --allow-empty");

                    if (baseCommit == null)
                    {
                        Console.WriteLine("    Failed to create base commit");
                        return PerformSimpleMerge(baseFile, mods, outputFile);
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

                        File.Copy(mods[i].FilePath, workFile, true);

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

                    if (mergeResult != null && mergeResult.Contains("CONFLICT"))
                    {
                        Console.WriteLine("    Merge has conflicts, attempting auto-resolution");
                        RunGitCommand(gitPath, tempRepo, "add .", true);
                        RunGitCommand(gitPath, tempRepo, "commit --no-edit -m \"resolved\"", true);
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

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            Console.WriteLine("    WARNING: Merged content is empty!");
                            return PerformSimpleMerge(baseFile, mods, outputFile);
                        }

                        File.WriteAllText(outputFile, content);

                        var outputInfo = new FileInfo(outputFile);
                        Console.WriteLine($"    Merge complete, output size: {outputInfo.Length} bytes");

                        return outputInfo.Length > 0;
                    }

                    Console.WriteLine("    Work file not found after merge");
                    return PerformSimpleMerge(baseFile, mods, outputFile);
                }
                finally
                {
                    try { Directory.Delete(tempRepo, true); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Git merge exception: {ex.Message}");
                return PerformSimpleMerge(baseFile, mods, outputFile);
            }
        }

        private bool PerformSimpleMerge(string baseFile, List<ModFileInfo> mods, string outputFile)
        {
            try
            {
                mods = mods.OrderBy(m => m.ModNumber).ToList();

                // Read base content
                string baseContent = File.ReadAllText(baseFile);

                // If only one mod, just check if it's different from base
                if (mods.Count == 1)
                {
                    string modContent = File.ReadAllText(mods[0].FilePath);
                    File.WriteAllText(outputFile, modContent != baseContent ? modContent : baseContent);
                    return true;
                }

                // For multiple mods: intelligent concatenation
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

        public string? RunGitCommand(string gitPath, string workingDir, string arguments, bool allowNonZeroExit = false)
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

        private string AutoResolveConflicts(string content)
        {
            var conflictPattern = new Regex(
                @"<<<<<<< .*?\n(.*?)\n=======\n(.*?)\n>>>>>>> .*?\n",
                RegexOptions.Singleline);

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
    }
}