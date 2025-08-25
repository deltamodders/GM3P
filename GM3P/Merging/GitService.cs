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
        bool PerformGitMerge(string baseFile, List<ModFileInfo> mods, string outputFile, string relativePath);
        string? RunGitCommand(string gitPath, string workingDir, string arguments, bool allowNonZeroExit = false);
    }

    public class GitService : IGitService
    {
        private readonly string _pwd;

        public GitService(string workingDirectory) => _pwd = workingDirectory;

        public string? FindGit()
        {
            try
            {
                string[] portableGitPaths = {
                    Path.Combine(_pwd, "git", "cmd", "git.exe"),
                    Path.Combine(_pwd, "git", "bin", "git.exe"),
                    Path.Combine(_pwd, "git", "mingw64", "bin", "git.exe")
                };
                foreach (string portableGit in portableGitPaths)
                {
                    if (!File.Exists(portableGit)) continue;
                    try
                    {
                        using var test = new Process();
                        test.StartInfo.FileName = portableGit;
                        test.StartInfo.Arguments = "--version";
                        test.StartInfo.CreateNoWindow = true;
                        test.StartInfo.UseShellExecute = false;
                        test.StartInfo.RedirectStandardOutput = true;
                        test.StartInfo.RedirectStandardError = true;
                        test.Start();
                        string v = test.StandardOutput.ReadToEnd();
                        test.WaitForExit(2000);
                        if (test.ExitCode == 0 && v.Contains("git version"))
                        {
                            Console.WriteLine($"  Found Git at: {portableGit}");
                            Console.WriteLine($"  Git version: {v.Trim()}");
                            return portableGit;
                        }
                    }
                    catch { /* try next */ }
                }

                using var proc = new Process();
                proc.StartInfo.FileName = OperatingSystem.IsWindows() ? "where" : "which";
                proc.StartInfo.Arguments = "git";
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();
                string? output = proc.StandardOutput.ReadLine();
                proc.WaitForExit(2000);
                if (!string.IsNullOrEmpty(output) && File.Exists(output.Trim()))
                {
                    Console.WriteLine($"  Found system Git at: {output.Trim()}");
                    return output.Trim();
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

        public bool PerformGitMerge(string baseFile, List<ModFileInfo> mods, string outputFile, string relativePath)
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

                    RunGitCommand(gitPath, tempRepo, "init -q");
                    RunGitCommand(gitPath, tempRepo, "config user.email \"gm3p@local\"");
                    RunGitCommand(gitPath, tempRepo, "config user.name \"GM3P\"");
                    RunGitCommand(gitPath, tempRepo, "config core.autocrlf false");

                    File.Copy(baseFile, workFile, true);
                    RunGitCommand(gitPath, tempRepo, "add .");
                    RunGitCommand(gitPath, tempRepo, "commit -q -m \"base\" --allow-empty");

                    var branches = new List<string>();
                    for (int i = 0; i < mods.Count; i++)
                    {
                        string branch = $"m{i}";
                        branches.Add(branch);
                        RunGitCommand(gitPath, tempRepo, $"checkout -q -b {branch} master");

                        File.Copy(mods[i].FilePath, workFile, true);
                        var info = new FileInfo(workFile);
                        Console.WriteLine($"    Mod {i} file size: {info.Length} bytes");

                        RunGitCommand(gitPath, tempRepo, "add .");
                        RunGitCommand(gitPath, tempRepo, $"commit -q -m \"mod{i}\" --allow-empty");
                        RunGitCommand(gitPath, tempRepo, "checkout -q master");
                    }

                    // Prefer incoming changes (theirs) → overlay-friendly
                    string list = string.Join(" ", branches);
                    Console.WriteLine($"    Merging branches: {list}");
                    // -X theirs ensures last-merge wins; still resolve any markers just in case
                    RunGitCommand(gitPath, tempRepo, $"merge {list} -X theirs --no-edit -m \"merged\"", allowNonZeroExit: true);

                    if (File.Exists(workFile))
                    {
                        string content = File.ReadAllText(workFile);
                        if (content.Contains("<<<<<<<"))
                        {
                            Console.WriteLine("    Auto-resolving conflict markers");
                            content = AutoResolveConflicts(content, relativePath);
                        }

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            Console.WriteLine("    WARNING: Merged content is empty!");
                            return PerformSimpleMerge(baseFile, mods, outputFile);
                        }

                        File.WriteAllText(outputFile, content);
                        var outInfo = new FileInfo(outputFile);
                        Console.WriteLine($"    Merge complete, output size: {outInfo.Length} bytes");
                        return outInfo.Length > 0;
                    }

                    Console.WriteLine("    Work file not found after merge");
                    return PerformSimpleMerge(baseFile, mods, outputFile);
                }
                finally
                {
                    try { Directory.Delete(tempRepo, true); } catch { }
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
                // Deterministic: last-mod-wins
                mods = mods.OrderBy(m => m.ModNumber).ToList();
                string content = File.ReadAllText(mods.Last().FilePath);
                File.WriteAllText(outputFile, content);
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
                using var p = new Process();
                p.StartInfo.FileName = gitPath;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.WorkingDirectory = workingDir;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                bool exited = p.WaitForExit(10000);

                if (!exited)
                {
                    Console.WriteLine($"      Git command timed out: {arguments}");
                    try { p.Kill(); } catch { }
                    return null;
                }

                if (p.ExitCode != 0 && !allowNonZeroExit)
                {
                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine($"      Git error: {error}");
                    return null;
                }

                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      Git command exception: {ex.Message}");
                return null;
            }
        }

        // Prefer incoming (“theirs”) for .gml; prefer “theirs” for other text as well (overlay-friendly).
        private string AutoResolveConflicts(string mergedText, string relativePath)
        {
            if (string.IsNullOrEmpty(mergedText)) return mergedText;

            bool isGml = relativePath.EndsWith(".gml", StringComparison.OrdinalIgnoreCase);

            var rx = new Regex(
                @"<<<<<<<[^\n]*\n(?<local>.*?)(?:\r?\n)=======(?:\r?\n)(?<remote>.*?)(?:\r?\n)>>>>>>>(?:[^\n]*)",
                RegexOptions.Singleline | RegexOptions.Compiled);

            string ReplaceOne(string input)
            {
                return rx.Replace(input, m =>
                {
                    string local  = m.Groups["local"].Value;
                    string remote = m.Groups["remote"].Value;

                    if (isGml) return string.IsNullOrWhiteSpace(remote) ? local : remote;

                    if (string.IsNullOrWhiteSpace(local))  return remote;
                    if (string.IsNullOrWhiteSpace(remote)) return local;
                    return remote; // last-mod wins
                });
            }

            string prev = mergedText, curr = ReplaceOne(mergedText);
            int guard = 0;
            while (!ReferenceEquals(prev, curr) && prev != curr && guard++ < 32)
            {
                prev = curr;
                curr = ReplaceOne(curr);
            }

            // Strip any leftover markers
            curr = Regex.Replace(curr, @"^<<<<<<<.*$\r?\n?", "", RegexOptions.Multiline);
            curr = Regex.Replace(curr, @"^=======\r?\n?", "", RegexOptions.Multiline);
            curr = Regex.Replace(curr, @"^>>>>>>>.*$\r?\n?", "", RegexOptions.Multiline);

            return curr;
        }
    }
}
