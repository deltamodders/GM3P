using GM3P.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using GM3P.FileSystem;
using InputSimulatorPro;
using InputSimulatorPro.Resources.Natives;
using InputSimulatorPro.Resources;


namespace GM3P.Manager
{
    public interface IModManager
    {
        Task PlayGame(string executablePath, string? inputList, GM3PConfig config);
        Task<List<string>> modList(GM3PConfig config);
        Task<bool> deleteMod(int modNo, GM3PConfig config);
    }
    public class ModManager : IModManager
    {
        public async Task<List<string>> modList(GM3PConfig config)
        {

            int modCount = 0;
            List<string> modList = [];
            List<string> modPaths = Directory.GetDirectories(Path.Combine(config.OutputPath!, "result"), "*", SearchOption.TopDirectoryOnly).ToList();
            if (Directory.Exists(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips")))
            {
                modPaths.AddRange(Directory.GetFiles(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips"), "*", SearchOption.TopDirectoryOnly));
            }
            foreach (var mod in modPaths) {
                string source;
                if (Directory.GetParent(mod)!.Name == "result") {
                    source = "GM3P 0 Result";
                }
                else
                {
                    source = "DeltaMod Package";
                }
                modCount++;
                string entry = string.Format("{0, 0} {1, 40} {2, 55}", source, modCount, Path.GetFileNameWithoutExtension(mod));
                modList.Add(entry);
            }
            return modList;
        }
        public async Task<bool> deleteMod(int modNo, GM3PConfig config)
        {
            List<string> modPaths = Directory.GetDirectories(Path.Combine(config.OutputPath!, "result"), "*", SearchOption.TopDirectoryOnly).ToList();
            if (Directory.Exists(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips")))
            {
                modPaths.AddRange(Directory.GetFiles(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips"), "*", SearchOption.TopDirectoryOnly));
            }
            if (modNo < 1 || modNo > modPaths.Count)
            {
                Console.WriteLine($"Invalid mod number: {modNo}");
                return false;
            }
            string modPath = modPaths[modNo - 1];
            try
            {
                if (File.Exists(modPath))
                {
                    File.Delete(modPath);
                }
                else if (Directory.Exists(modPath))
                {
                    Directory.Delete(modPath, true);
                }
                Console.WriteLine($"Deleted mod: {Path.GetFileNameWithoutExtension(modPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete mod: {ex.Message}");
                return false;
            }
        }
        public async Task PlayGame(string executablePath, string? inputList, GM3PConfig config)
        {
            try
            {
                string executableDirectory = Path.GetDirectoryName(executablePath)!;
                string executableName = Path.GetFileName(executablePath);
                // Implementation for playing the game goes here
                using (var process = new Process())
                {
                    process.StartInfo.WorkingDirectory = executableDirectory;
                    process.StartInfo.FileName = $"{executablePath}";
                    process.StartInfo.Arguments =
                            $"";
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    char[] inputs = ['a'];
                    Console.WriteLine($"Input List: {Path.GetFileName(inputList)}");
                    if (File.Exists(inputList))
                    {
                        string inputContent = File.ReadAllText(inputList);
                        Console.WriteLine(inputContent);
                        char[] a = inputContent.ToCharArray();
                        Array.Resize(ref inputs, a.Length);
                        inputs = a;
                    }
                    if (inputs?.Length>10)
                    {
                        for (int i = 0; i < inputs.Length; i++)
                        {
                            InputSimulator.Keyboard.TextEntry($"{inputs[i]}");
                            Thread.Sleep(33);
                        }
                    }
                    string output = await process.StandardOutput.ReadToEndAsync();
                    Console.WriteLine(output);
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start game: {ex.Message}");
            }
        }



    }
}
