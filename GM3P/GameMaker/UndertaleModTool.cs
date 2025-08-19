using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GM3P.Data;

namespace GM3P.GameMaker
{
    public interface IUndertaleModTool
    {
        Task RunExportScripts(string dataWin, GM3PConfig config, int modNumber = -1);
        Task RunImportScripts(string dataWin, string[] scriptNames, GM3PConfig config);
        Task RunScript(string dataWin, string scriptName, GM3PConfig config);
    }

    public class UndertaleModTool : IUndertaleModTool
    {
        public async Task RunExportScripts(string dataWin, GM3PConfig config, int modNumber = -1)
        {
            // Better detection of vanilla vs mod
            bool isVanilla = false;
            string detectedModNumber = "unknown";
            string dataWinDir = Path.GetDirectoryName(dataWin) ?? "";

            // If modNumber was passed directly, use it
            if (modNumber >= 0)
            {
                isVanilla = (modNumber == 0);
                detectedModNumber = modNumber.ToString();
            }
            else
            {
                // Try to detect from path
                if (dataWinDir.Contains("xDeltaCombiner"))
                {
                    var parts = dataWinDir.Split(Path.DirectorySeparatorChar);
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "xDeltaCombiner" && i + 2 < parts.Length)
                        {
                            // parts[i+1] is chapter, parts[i+2] is mod number
                            detectedModNumber = parts[i + 2];
                            if (int.TryParse(detectedModNumber, out int parsedModNumber))
                            {
                                isVanilla = (parsedModNumber == 0);
                                modNumber = parsedModNumber;
                            }
                            break;
                        }
                    }
                }
            }

            Console.WriteLine($"    Mod slot {detectedModNumber}: {(isVanilla ? "vanilla (full export)" : "mod (smart export)")}");

            using (var modToolProc = new Process())
            {
                string scriptsToRun;

                if (isVanilla)
                {
                    // For vanilla, export everything as baseline
                    scriptsToRun = " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\"" +
                                  " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllCode.csx\"" +
                                  " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAssetOrder.csx\"";
                }
                else
                {
                    // For mods, check if ExportModifiedOnly.csx exists
                    string modifiedOnlyScript = Path.Combine(config.WorkingDirectory, "UTMTCLI", "Scripts", "ExportModifiedOnly.csx");
                    if (File.Exists(modifiedOnlyScript))
                    {
                        // Use the optimized export
                        scriptsToRun = " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportModifiedOnly.csx\"";
                    }
                    else
                    {
                        // Fall back to full export
                        Console.WriteLine("      Note: ExportModifiedOnly.csx not found, using full export");
                        scriptsToRun = " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\"" +
                                      " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllCode.csx\"" +
                                      " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAssetOrder.csx\"";
                    }
                }

                if (OperatingSystem.IsWindows())
                {
                    modToolProc.StartInfo.FileName = config.ModToolPath;
                    modToolProc.StartInfo.Arguments =
                        "load \"" + dataWin + "\" --verbose --output \"" + dataWin + "\"" + scriptsToRun;
                }
                else
                {
                    modToolProc.StartInfo.FileName = "/bin/bash";
                    modToolProc.StartInfo.Arguments =
                        "-c \"" + config.ModToolPath + " load '" + dataWin + "' --verbose --output '" + dataWin + "'" +
                        scriptsToRun.Replace("\"", "'") + "\"";
                }

                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.StartInfo.UseShellExecute = false;
                modToolProc.StartInfo.RedirectStandardOutput = true;
                modToolProc.StartInfo.RedirectStandardError = true;

                // Set working directory to help scripts find files
                modToolProc.StartInfo.WorkingDirectory = Path.GetDirectoryName(dataWin);

                modToolProc.Start();

                // Read output asynchronously to prevent deadlocks
                var outputTask = modToolProc.StandardOutput.ReadToEndAsync();
                var errorTask = modToolProc.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await modToolProc.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                if (!string.IsNullOrWhiteSpace(output))
                    Console.WriteLine(output);
                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine($"      Export warnings: {error}");
            }
        }

        public async Task RunImportScripts(string dataWin, string[] scriptNames, GM3PConfig config)
        {
            if (!File.Exists(dataWin))
            {
                Console.WriteLine($"ERROR: data.win not found at {dataWin}");
                return;
            }

            // Get the working directory
            var workingDir = Path.GetDirectoryName(dataWin);
            var originalDir = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(workingDir!);

                using (var proc = new Process())
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var args = "load \"" + dataWin + "\" --verbose --output \"" + dataWin + "\"";
                        foreach (var script in scriptNames)
                            args += " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/" + script + "\"";

                        proc.StartInfo.FileName = config.ModToolPath;
                        proc.StartInfo.Arguments = args;
                    }
                    else
                    {
                        var args = config.ModToolPath + " load '" + dataWin + "' --verbose --output '" + dataWin + "'";
                        foreach (var script in scriptNames)
                            args += " --scripts '" + config.WorkingDirectory + "/UTMTCLI/Scripts/" + script + "'";

                        proc.StartInfo.FileName = "/bin/bash";
                        proc.StartInfo.Arguments = "-c \"" + args + "\"";
                    }

                    proc.StartInfo.CreateNoWindow = false;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.WorkingDirectory = workingDir;

                    proc.Start();

                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    var errorTask = proc.StandardError.ReadToEndAsync();

                    await Task.WhenAll(outputTask, errorTask);
                    await proc.WaitForExitAsync();

                    var output = await outputTask;
                    var error = await errorTask;

                    Console.WriteLine(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"STDERR: {error}");
                    }

                    if (proc.ExitCode != 0)
                    {
                        Console.WriteLine($"WARNING: UndertaleModTool exited with code {proc.ExitCode}");
                    }
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        public async Task RunScript(string dataWin, string scriptName, GM3PConfig config)
        {
            await RunImportScripts(dataWin, new[] { scriptName }, config);
        }
    }
}