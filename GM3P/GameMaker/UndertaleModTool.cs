using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GM3P.Data;

namespace GM3P.GameMaker
{
    public interface IUndertaleModTool
    {
        Task RunExportScripts(string dataWin, GM3PConfig config);
        Task RunImportScripts(string dataWin, string[] scriptNames, GM3PConfig config);
        Task RunScript(string dataWin, string scriptName, GM3PConfig config);
    }

    public class UndertaleModTool : IUndertaleModTool
    {
        public async Task RunExportScripts(string dataWin, GM3PConfig config)
        {
            // EXACTLY like the original
            using (var modToolProc = new Process())
            {
                if (OperatingSystem.IsWindows())
                {
                    modToolProc.StartInfo.FileName = config.ModToolPath;
                    modToolProc.StartInfo.Arguments =
                        "load \"" + dataWin + "\" --verbose --output \"" + dataWin + "\"" +
                        " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx\"" +
                        " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllCode.csx\"" +
                        " --scripts \"" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAssetOrder.csx\"";
                }
                else
                {
                    modToolProc.StartInfo.FileName = "/bin/bash";
                    modToolProc.StartInfo.Arguments =
                        "-c \"" + config.ModToolPath + " load '" + dataWin + "' --verbose --output '" + dataWin + "'" +
                        " --scripts '" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllTexturesGrouped.csx'" +
                        " --scripts '" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAllCode.csx'" +
                        " --scripts '" + config.WorkingDirectory + "/UTMTCLI/Scripts/ExportAssetOrder.csx'\"";
                }

                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.StartInfo.UseShellExecute = false;
                modToolProc.StartInfo.RedirectStandardOutput = true;
                modToolProc.Start();

                // EXACTLY like original - synchronous read
                Console.WriteLine(modToolProc.StandardOutput.ReadToEnd());
                modToolProc.WaitForExit();
            }

            await Task.CompletedTask; // Keep async signature for interface
        }

        public async Task RunImportScripts(string dataWin, string[] scriptNames, GM3PConfig config)
        {
            // Build arguments EXACTLY like original
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
                proc.Start();

                string output = proc.StandardOutput.ReadToEnd();
                string errorOutput = proc.StandardError.ReadToEnd();

                Console.WriteLine(output);
                if (!string.IsNullOrEmpty(errorOutput))
                    Console.WriteLine($"Error: {errorOutput}");

                proc.WaitForExit();
            }

            await Task.CompletedTask;
        }

        public async Task RunScript(string dataWin, string scriptName, GM3PConfig config)
        {
            await RunImportScripts(dataWin, new[] { scriptName }, config);
        }
    }
}