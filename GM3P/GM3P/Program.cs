using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GM3P.Cache;
using GM3P.Core;
using GM3P.FileSystem;
using GM3P.GameMaker;
using GM3P.Logging;
using GM3P.Merging;
using GM3P.Patching;

namespace GM3P
{
    class Program
    {
        private const double Version = 0.6;
        private static IGM3POrchestrator? _orchestrator;
        private static IConfigurationService? _config;

        static async Task Main(string[] args)
        {
            Console.WriteLine($"GM3P v{Version}.0-alpha3");

            // Setup services manually (no DI container)
            SetupServices();

            // Setup logging
            var logPath = SetupLogging(_config!.Config.OutputPath);

            using (var consoleLogger = new ConsoleLogger(logPath))
            {
                try
                {
                    if (args == null || args.Length == 0)
                    {
                        await RunConsoleApp();
                    }
                    else
                    {
                        await RunCommand(args);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fatal error: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    Environment.Exit(1);
                }
            }
        }

        static void SetupServices()
        {
            // Create all services manually
            var config = new ConfigurationService();
            var directoryManager = new DirectoryManager();
            var fileLinker = new FileLinker();
            var hashCache = new HashCache();
            var exportCache = new ExportCache(fileLinker);
            var assetHelper = new AssetHelper();
            var pngUtils = new PngUtils(hashCache);
            var modTool = new UndertaleModTool();
            var assetOrderMerger = new AssetOrderMerger(directoryManager);
            var gitService = new GitService(config.Config.WorkingDirectory ?? Directory.GetCurrentDirectory());
            var modCombiner = new ModCombiner(
                directoryManager, fileLinker, hashCache, pngUtils,
                assetHelper, assetOrderMerger, gitService, modTool);
            var patchService = new PatchService(directoryManager);

            _orchestrator = new GM3POrchestrator(
                config, directoryManager, fileLinker, hashCache,
                exportCache, patchService, modCombiner, modTool);

            _config = config;
        }

        static string SetupLogging(string? outputPath)
        {
            outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output");

            var logsDir = Path.Combine(outputPath, "Cache", "Logs");
            Directory.CreateDirectory(logsDir);

            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            var logFile = Path.Combine(logsDir, $"{timestamp}.txt");
            File.Create(logFile).Close();

            return logFile;
        }

        static async Task RunCommand(string[] args)
        {
            var command = args[0].ToLower();

            switch (command)
            {
                case "masspatch":
                    await HandleMassPatch(args);
                    break;

                case "compare":
                    await HandleCompare(args);
                    break;

                case "result":
                    await HandleResult(args);
                    break;

                case "console":
                    await RunConsoleApp();
                    break;

                case "clear":
                    HandleClear(args);
                    break;

                case "help":
                    ShowHelp(args);
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Use 'GM3P.exe help' for available commands");
                    break;
            }
        }

        static async Task HandleMassPatch(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: GM3P.exe massPatch [VanillaPath] [GameEngine] [ModAmount] [PatchPaths] [OutputPath?]");
                return;
            }

            _config!.UpdateConfiguration(c =>
            {
                c.VanillaPath = args[1].Replace("\"", "");
                c.GameEngine = args[2];
                c.ModAmount = int.Parse(args[3]);

                if (args.Length > 5)
                    c.OutputPath = args[5];
            });

            var patchPaths = args[4].Split("::").ToArray();
            await _orchestrator!.ExecuteMassPatch(patchPaths);
        }

        static async Task HandleCompare(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: GM3P.exe compare [ModAmount] [Dump?] [Import?] [OutputPath?]");
                return;
            }

            _config!.UpdateConfiguration(c =>
            {
                c.ModAmount = int.Parse(args[1]);

                if (args.Length > 4)
                    c.OutputPath = args[4];
            });

            bool shouldDump = args.Length <= 2 || args[2].ToLower() == "true";
            bool shouldImport = args.Length > 3 && args[3].ToLower() == "true";

            if (shouldDump)
                await _orchestrator!.ExecuteDump();

            await _orchestrator!.ExecuteCompareCombine();

            if (shouldImport)
                await _orchestrator!.ExecuteImport();
        }

        static async Task HandleResult(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: GM3P.exe result [ModName] [Combined?] [ModAmount?] [OutputPath?]");
                return;
            }

            string modName = args[1];

            if (args.Length > 2)
            {
                _config!.UpdateConfiguration(c =>
                {
                    c.Combined = bool.Parse(args[2]);

                    if (args.Length > 3)
                        c.ModAmount = int.Parse(args[3]);

                    if (args.Length > 4)
                        c.OutputPath = args[4];
                });
            }

            await _orchestrator!.ExecuteResult(modName);
        }

        static void HandleClear(string[] args)
        {
            string target = args.Length > 1 ? args[1] : "runningCache";
            _orchestrator!.Clear(target);
        }

        static async Task RunConsoleApp()
        {
            Console.WriteLine("Read the README for Operating Instructions\n");

            // Original message
            Console.WriteLine("Insert the path to the vanilla data.win, or type \"skip\" if want skip to compare and combine:");
            var vanillaPath = Console.ReadLine()?.Replace("\"", "");

            if (vanillaPath != "skip")
            {
                _config!.UpdateConfiguration(c => c.VanillaPath = vanillaPath);

                // Original message
                Console.WriteLine("Type however many mods you want to patch (If you are patching multiple chapters, this would be the amount of mods for a single chapter): ");
                if (int.TryParse(Console.ReadLine(), out var modAmount))
                {
                    _config!.UpdateConfiguration(c => c.ModAmount = modAmount);
                }

                // Original message
                Console.WriteLine("Now Enter in the patches, one at a time (If you are doing multi-chapter patching, do the mods for the root first): ");

                // Build the patch array in the format expected
                var chapterPatches = new List<string>();

                // For each chapter (determined by vanilla path)
                var vanillaFiles = new DirectoryManager().FindDataWinFiles(vanillaPath);
                for (int chapter = 0; chapter < vanillaFiles.Count; chapter++)
                {
                    if (chapter > 0)
                    {
                        Console.WriteLine($"Enter patches for Chapter {chapter}:");
                    }

                    var patches = new List<string> { "", "" }; // Start with two empty entries for compatibility

                    for (int modNumber = 1; modNumber <= modAmount; modNumber++)
                    {
                        Console.Write($"  Patch for Mod {modNumber}: ");
                        string patch = Console.ReadLine()?.Replace("\"", "") ?? "";
                        patches.Add(patch);
                    }

                    chapterPatches.Add(string.Join(",", patches));
                }

                if (chapterPatches.Count > 0)
                {
                    await _orchestrator!.ExecuteMassPatch(chapterPatches.ToArray());
                }

                // Original message after patching
                Console.WriteLine("\nMass Patch complete, continue or use the compare command to combine mods");
            }

            // Original messages for mod tool
            Console.WriteLine("\nEnter in the Mod Tool (e.g. UnderTaleModTool for GameMaker Games). If you want to use the included tool, just hit enter. If you want to manually dump and import enter \"skip\"");
            Console.WriteLine("If you don't want to combine patches and just wanted to apply them, you may enter \"noCombine\"");

            var modTool = Console.ReadLine();

            if (modTool == "noCombine")
            {
                // Exit early if user doesn't want to combine
                return;
            }

            if (string.IsNullOrEmpty(modTool))
            {
                // User pressed enter, use default tool
                // Config already has default path set
            }
            else if (modTool == "skip")
            {
                _config!.UpdateConfiguration(c => c.ModToolPath = "skip");

                // Original manual dump instructions
                Console.WriteLine("In order to dump manually, load up the data.win in each of the /xDeltaCombiner/ subfolders into the GUI version of UTMT and run the script ExportAllCode.csx. Select \"C:/xDeltaCombiner/*currentsubfolder*/Objects/\" as your destination. Once finished, exit without saving.");
                Console.WriteLine("Press Enter when done with the above instructions");
                Console.ReadLine();
            }
            else
            {
                _config!.UpdateConfiguration(c => c.ModToolPath = modTool);
            }

            if (modTool != "skip")
            {
                Console.WriteLine("Starting dump, this may take up to a minute per mod (and vanilla)");
                await _orchestrator!.ExecuteDump();
                Console.WriteLine("The dumping process(es) are finished. Hit Enter to Continue.");
                Console.ReadLine();
            }

            await _orchestrator!.ExecuteCompareCombine();
            Console.WriteLine("Comparing is done. Hit Enter to Continue.");
            Console.ReadLine();

            await _orchestrator!.ExecuteImport();

            // Original message
            Console.WriteLine("To save your modpack or modset, name it: ");
            var modName = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(modName))
            {
                await _orchestrator!.ExecuteResult(modName);
            }

            // Original cleanup message
            Console.WriteLine("Press Enter To Clean up (Will delete output/xDeltaCombiner) and exit");
            Console.ReadLine();
            _orchestrator!.Clear();
            Environment.Exit(1); // Original behavior
        }

        static void ShowHelp(string[] args)
        {
            if (args.Length > 1)
            {
                ShowCommandHelp(args[1]);
            }
            else
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  help       - Display command help");
                Console.WriteLine("  massPatch  - Patch multiple data.win files");
                Console.WriteLine("  compare    - Compare and combine mods");
                Console.WriteLine("  result     - Create final modpack");
                Console.WriteLine("  console    - Launch interactive console");
                Console.WriteLine("  clear      - Clear temporary files");
                Console.WriteLine("\nUse 'GM3P.exe help [command]' for detailed help");
            }
        }

        static void ShowCommandHelp(string command)
        {
            switch (command.ToLower())
            {
                case "masspatch":
                    Console.WriteLine("\nMassPatch Command:");
                    Console.WriteLine("  Patches multiple data.win files with mods");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe massPatch [VanillaPath] [GameEngine] [ModAmount] [PatchPaths] [OutputPath?]");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  VanillaPath - Path to vanilla game or data.win");
                    Console.WriteLine("  GameEngine  - Game engine type (GM for GameMaker)");
                    Console.WriteLine("  ModAmount   - Number of mods to patch");
                    Console.WriteLine("  PatchPaths  - Mod file paths (:: for chapters, , for mods)");
                    Console.WriteLine("  OutputPath  - Optional output directory");
                    break;

                case "compare":
                    Console.WriteLine("\nCompare Command:");
                    Console.WriteLine("  Compares and combines mod objects");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe compare [ModAmount] [Dump?] [Import?] [OutputPath?]");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  ModAmount  - Number of mods");
                    Console.WriteLine("  Dump       - Whether to dump objects (true/false)");
                    Console.WriteLine("  Import     - Whether to import objects (true/false)");
                    Console.WriteLine("  OutputPath - Optional output directory");
                    break;

                case "result":
                    Console.WriteLine("\nResult Command:");
                    Console.WriteLine("  Creates final modpack files");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe result [ModName] [Combined?] [ModAmount?] [OutputPath?]");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  ModName    - Name for the modpack");
                    Console.WriteLine("  Combined   - Whether mods were combined (true/false)");
                    Console.WriteLine("  ModAmount  - Number of mods");
                    Console.WriteLine("  OutputPath - Optional output directory");
                    break;

                case "clear":
                    Console.WriteLine("\nClear Command:");
                    Console.WriteLine("  Clears temporary files and directories");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe clear [Target?] [OutputPath?]");
                    Console.WriteLine("\nTargets:");
                    Console.WriteLine("  runningCache - Clear xDeltaCombiner and running cache (default)");
                    Console.WriteLine("  cache        - Clear all cache");
                    Console.WriteLine("  output       - Clear entire output directory");
                    Console.WriteLine("  modpacks     - Clear result directory");
                    break;

                default:
                    Console.WriteLine($"No help available for command: {command}");
                    break;
            }
        }
    }
}