using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GM3P.Cache;
using GM3P.Core;
using GM3P.Data;
using GM3P.FileSystem;
using GM3P.GameMaker;
using GM3P.Logging;
using GM3P.Merging;
using GM3P.Patching;
using GM3P.Manager;

namespace GM3P
{
    class Program
    {
        private const double Version = 1.0;
        private static IGM3POrchestrator? _orchestrator;
        private static IConfigurationService? _config;
        private static IDeltaModPackageService? _deltaModPackageService;

        static async Task Main(string[] args)
        {
            Console.WriteLine($"GM3P v{Version}.0-beta3");

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
                        await RunAppVersion();
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
            var modManager = new ModManager();
            var install = new Install(directoryManager);
            var deltaModPackageService = new DeltaModPackageService();
            _orchestrator = new GM3POrchestrator(
                config, directoryManager, fileLinker, hashCache,
                exportCache, patchService, modCombiner, modTool, modManager, install, deltaModPackageService);

            _config = config;
            _deltaModPackageService = deltaModPackageService;
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

        static string[] opArgs = { };
        static string[] reqArgs = { };
        static async Task seperateOptionArgs(string[] args)
        {

            var opArgParse = 0;
            string[] singleOptions = { "-help", "v", "-lose", "-bake" };
            int opArgCount = 0;
            int reqArgCount = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    Array.Resize(ref opArgs, opArgCount + 1);
                    opArgs[opArgCount] = args[i];
                    foreach (string singleOption in singleOptions)
                    {
                        if (!args[i].EndsWith(singleOption))
                        {
                            opArgParse++;
                        }
                        else
                        {
                            opArgCount++;
                        }
                    }

                }
                else if (!args[i].StartsWith("-"))
                {
                    if (opArgParse == 0)
                    {
                        Array.Resize(ref reqArgs, reqArgCount + 1);
                        reqArgs[reqArgCount] = args[i];
                        reqArgCount++;
                    }
                    else
                    {
                        opArgs[opArgCount] = opArgs[opArgCount] + " " + args[i];
                        opArgParse--;
                        opArgCount++;
                    }
                }
            }

        }

        static async Task RunCommand(string[] args)
        {
            await seperateOptionArgs(args);
            var command = reqArgs[0].ToLower();

            switch (command)
            {
                case "exit":
                    break;
                case "config":
                    await HandleConfig(args);
                    break;
                case "masspatch":
                    await HandleMassPatch(reqArgs, opArgs);
                    break;

                case "compare":
                    await HandleCompare(reqArgs, opArgs);
                    break;

                case "result":
                    await HandleResult(reqArgs, opArgs);
                    break;

                case "console":
                    if (args.Length > 1)
                    {
                        var loadPath = args.Length > 1 ? args[1] : null;
                        _config?.LoadConfiguration(loadPath);
                        Console.WriteLine($"Configuration loaded from {(loadPath ?? "default path")}");
                    }
                    await RunConsoleApp();
                    break;

                case "clear":
                    HandleClear(args);
                    break;

                case "version":
                    Console.WriteLine($"GM3P v{Version}.0-beta2");
                    break;

                case "metadata":
                    await HandleMetadata(args);
                    break;

                case "play":
                    await HandlePlay(reqArgs, opArgs);
                    break;

                case "import":
                    await HandleInstall(reqArgs, opArgs);
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
        static async Task HandleConfig(string[] args)
        {
            await seperateOptionArgs(args);
            if (reqArgs.Length < 3)
            {
                Console.WriteLine("Usage: GM3P.exe config [update] c.[setting] [Value] save? [configPath?]");
                return;
            }
            var subcommand = args[1].ToLower();
            var savePath = args.Length > 4 ? args[5] : null;
            switch (subcommand)
            {
                case "update":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Usage: GM3P.exe config update c.[setting] [Value] save? [configPath?]");
                        return;
                    }

                    if (File.Exists(savePath))
                    {
                        _config?.LoadConfiguration(savePath);
                        Console.WriteLine($"Configuration loaded from {savePath}");
                    }
                    var setting = args[2];
                    var value = args[3];
                    switch (setting)
                    {
                        case "c.vanillapath":
                            _config?.UpdateConfiguration(c => c.VanillaPath = value);
                            break;
                        case "c.outputpath":
                            _config?.UpdateConfiguration(c => c.OutputPath = value);
                            break;
                        case "c.deltapatcherpath":
                            _config?.UpdateConfiguration(c => c.DeltaPatcherPath = value);
                            break;
                        case "c.modtoolpath":
                            _config?.UpdateConfiguration(c => c.ModToolPath = value);
                            break;
                        case "c.gameengine":
                            _config?.UpdateConfiguration(c => c.GameEngine = value);
                            break;
                        case "c.modamount":
                            _config?.UpdateConfiguration(c => c.ModAmount = int.Parse(value));
                            break;
                        case "c.chapteramount":
                            _config?.UpdateConfiguration(c => c.ChapterAmount = int.Parse(value));
                            break;
                        case "c.combined":
                            _config?.UpdateConfiguration(c => c.Combined = bool.Parse(value));
                            break;
                        case "c.enablefastcombiner":
                            _config?.UpdateConfiguration(c => c.EnableFastCombiner = bool.Parse(value));
                            break;
                        case "c.combinertool":
                            _config?.UpdateConfiguration(c => c.CombinerTool = int.Parse(value));
                            break;
                        case "c.mergeMethod":
                            _config?.UpdateConfiguration(c => c.mergeMethod = value);
                            break;
                        case "c.utmtversion":
                            _config?.UpdateConfiguration(c => c.UTMTversion = int.Parse(value));
                            break;
                        case "c.treatg3mpatchaszip":
                            _config?.UpdateConfiguration(c => c.TreatG3MPatchAsZip = bool.Parse(value));
                            break;
                        case "c.verboselogging":
                            _config?.UpdateConfiguration(c => c.verboseLogging = bool.Parse(value));
                            break;
                        case "c.cacheenabled":
                            _config?.UpdateConfiguration(c => c.CacheEnabled = bool.Parse(value));
                            break;
                        case "c.cachespritesenabled":
                            _config?.UpdateConfiguration(c => c.CacheSpritesEnabled = bool.Parse(value));
                            break;
                        case "c.exportcachecapmb":
                            _config?.UpdateConfiguration(c => c.ExportCacheCapMB = int.Parse(value));
                            break;
                        case "c.xdeltaconcurrency":
                            _config?.UpdateConfiguration(c => c.XDeltaConcurrency = int.Parse(value));
                            break;
                        default:
                            Console.WriteLine($"Unknown setting: {setting}");
                            Console.WriteLine("Use 'GM3P.exe help config' for usage");
                            break;
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown config subcommand: {subcommand}");
                    Console.WriteLine("Use 'GM3P.exe help config' for usage");
                    break;
            }
            if (args.Length > 4)
            {

                _config?.SaveConfiguration(savePath);
                Console.WriteLine($"Configuration saved to {(savePath ?? "default path")}");
                return;
            }
        }
        static async Task HandleMassPatch(string[] regargs, string[] opargs)
        {
            await seperateOptionArgs(regargs);
            if (regargs.Length < 4)
            {
                Console.WriteLine("Usage: GM3P.exe massPatch [VanillaPath] [ModAmount] [PatchPaths] --config? <ConfigPath> --relative? <relativepath>");
                return;
            }


            _config!.UpdateConfiguration(c =>
            {
                c.VanillaPath = regargs[1].Replace("\"", "");
                c.ModAmount = int.Parse(regargs[2]);
            });
            string? releativePath = null;
            bool packageornot = false;
            var patchPaths = regargs[3].Replace("\"", "").Split("::").ToArray();
            for (int i = 0; i < opargs.Length; i++)
            {
                if (opargs[i].StartsWith("--config"))
                {
                    _config!.UpdateConfiguration(c =>
                    {
                        _config?.LoadConfiguration(opargs[i].Replace("--config ", ""));
                        Console.WriteLine($"Configuration loaded from {(opargs[i].Replace("--config ", "") ?? "default path")}");
                    });
                }
                if (opargs[i].StartsWith("--relative"))
                {
                    releativePath = opargs[i].Replace("--relative", "").Trim().Replace("\"", "");
                    Console.WriteLine($"Relative path set to {releativePath}");
                    patchPaths = regargs[3].Replace("\"", "").Replace(".\\", releativePath + "\\").Replace("./", releativePath + "/").Split("::").ToArray();
                    //regargs[3].Replace("::.\\", releativePath + "\\");
                    //regargs[3].Replace("::./", releativePath + "/");
                    Console.WriteLine(patchPaths[0]);

                }
                if (opargs[i].StartsWith("--package"))
                {
                    packageornot = true;
                }
            }




            for (int i = 0; i < patchPaths.Length; i++)
            {
                Console.WriteLine(patchPaths[i]);
            }
            await _orchestrator!.ExecuteMassPatch(patchPaths);
        }

        static async Task HandleCompare(string[] reqargs, string[] opargs)
        {
            await seperateOptionArgs(reqargs);
            if (reqargs.Length < 2)
            {
                Console.WriteLine("Usage: GM3P.exe compare [ModAmount] --noDump? --noImport? --config? <ConfigPath?>");
                return;
            }

            _config!.UpdateConfiguration(c =>
            {
                c.ModAmount = int.Parse(reqargs[1]);
            });

            bool shouldDump = true;
            bool shouldImport = true;
            for (int i = 0; i < opargs.Length; i++)
            {
                if (opargs[i].StartsWith("--config"))
                {
                    _config!.UpdateConfiguration(c =>
                    {
                        _config?.LoadConfiguration(opargs[i].Replace("--config ", ""));
                        Console.WriteLine($"Configuration loaded from {(opargs[i].Replace("--config ", "") ?? "default path")}");
                    });
                }
                if (opargs[i].StartsWith("--noDump"))
                {
                    shouldDump = false;
                }
                if (opargs[i].StartsWith("--noImport"))
                {
                    shouldImport = false;
                }
            }
            if (shouldDump)
                await _orchestrator!.ExecuteDump();

            await _orchestrator!.ExecuteCompareCombine();

            if (shouldImport)
                await _orchestrator!.ExecuteImport();
        }

        static async Task HandleResult(string[] reqargs, string[] opargs)
        {
            if (reqargs.Length < 2)
            {
                Console.WriteLine("Usage: GM3P.exe result [ModName] --notCombined? <ModAmount> --config? <ConfigPath> --lose");
                return;
            }

            string modName = reqargs[1];
            bool win = true;
            for (int i = 0; i < opargs.Length; i++)
            {
                if (opArgs[i].StartsWith("--notCombined"))
                {
                    _config!.UpdateConfiguration(c =>
                    {
                        c.Combined = false;
                        c.ModAmount = int.Parse(opargs[i].Replace("--notCombined ", ""));
                    });
                }
                if (opArgs[i].StartsWith("--config"))
                {
                    _config!.UpdateConfiguration(c =>
                    {
                        _config?.LoadConfiguration(opArgs[i].Replace("--config ", ""));
                        Console.WriteLine($"Configuration loaded from {(opArgs[i].Replace("--config ", "") ?? "default path")}");
                    });
                }
                if (opargs[i].StartsWith("--modName "))
                {
                    modName = opargs[i].Replace("--modName ", "");
                }
                if (opargs[i].StartsWith("--lose"))
                {
                    win = false;
                }
            }
            _config!.UpdateConfiguration(c => c.win = win);
            await _orchestrator!.ExecuteResult(modName);
        }
        static async Task HandleMetadata(string[] args)
        {
            var subcommand = args[1].ToLower();
            var type = args[2].ToLower();

            if (subcommand == "parse")
            {
                if (type == "xml")
                {
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: GM3P.exe metadata parse [XMLPath]");
                        return;
                    }
                    string xmlPath = args[3];
                    _deltaModPackageService!.ParseXML(xmlPath);
                }
                else if (type == "json")
                {
                    _deltaModPackageService!.LoadJSON(args[3]);
                }
                else
                {
                    Console.WriteLine($"Unknown metadata type: {type}");
                }
            }
            else if (subcommand == "export")
            {
                if (type == "xml")
                {
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: GM3P.exe metadata export [XMLPath] [result]");
                        return;
                    }
                    string xmlPath = args[3];
                    _config?.UpdateConfiguration(c => c.ChapterAmount = int.Parse(args[5]));
                    await _deltaModPackageService!.ExportXML(args[3], args[4], _config!.Config);
                }
                else if (type == "json")
                {
                    _deltaModPackageService!.SaveJSON(args[3]);
                }
                else
                {
                    Console.WriteLine($"Unknown metadata type: {type}");
                }
            }
            else if (subcommand == "update")
            {
                if (type == "json")
                {
                    var color = new DeltaModPackageColor();
                    var neededFiles = new DeltaModPackageneededFiles();
                    var metadata = new DeltaModPackagemetadata();
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Usage: GM3P.exe metadata update json [Key] [Value]");
                        return;
                    }
                    string key = args[3];
                    string value = args[4];
                    switch (key)
                    {
                        case "name":
                            metadata.name = value;
                            break;
                        case "author":
                            metadata.author = value.Split(",").Select(a => a.Trim()).ToArray();
                            break;
                        case "version":
                            metadata.version = value;
                            break;
                        case "description":
                            metadata.description = value;
                            break;
                        case "url":
                            metadata.url = value;
                            break;
                        case "game":
                            metadata.game = value;
                            break;
                        case "packageID":
                            metadata.packageID = value;
                            break;
                        case "color":
                            var colorValues = value.Split(",").Select(c => c.Trim()).ToArray();
                            if (colorValues.Length == 3)
                            {
                                color.r = colorValues[0];
                                color.g = colorValues[1];
                                color.b = colorValues[2];
                                metadata.color = new Dictionary<string, DeltaModPackageColor> { { "primary", color } };
                            }
                            else
                            {
                                Console.WriteLine("Color value must be in the format 'R,G,B'");
                            }
                            break;
                        case "neededFiles":
                            var fileValues = value.Split(",").Select(f => f.Trim()).ToArray();
                            if (fileValues.Length == 2)
                            {
                                neededFiles.file = fileValues[0];
                                neededFiles.checksum = fileValues[1];
                                // Assuming you want to add this to a list of needed files in metadata
                                // You might need to adjust this based on your actual data structure
                                // metadata.neededFiles.Add(neededFiles);
                            }
                            else
                            {
                                Console.WriteLine("Needed files value must be in the format 'file,checksum'");
                            }
                            break;
                        case "UTMTVersion":
                            _deltaModPackageService.UpdateJSON(c => c.UTMTversion = Convert.ToInt32(value));
                            break;
                        case "deltaruneTargetVersion":
                            _deltaModPackageService.UpdateJSON(c => c.deltaruneTargetVersion = value);
                            break;
                        default:
                            Console.WriteLine($"Unknown metadata key: {key}");
                            break;
                    }
                    DeltaModPackagemetadata updatedMetadata = new DeltaModPackagemetadata
                    {
                        name = metadata.name,
                        version = metadata.version,
                        author = metadata.author,
                        description = metadata.description,
                        url = metadata.url,
                        game = metadata.game,
                        packageID = metadata.packageID,
                        color = metadata.color
                    };
                    DeltaModPackageneededFiles updatedNeededFiles = new DeltaModPackageneededFiles
                    {
                        file = neededFiles.file,
                        checksum = neededFiles.checksum
                    };
                    DeltaModPackageColor updatedColor = new DeltaModPackageColor
                    {
                        r = color.r,
                        g = color.g,
                        b = color.b
                    };
                    DeltaModPackageExporter exporter = new DeltaModPackageExporter
                    {
                        tool = "GM3P"
                    };
                }
                else if (type == "xml")
                {
                    await _deltaModPackageService.UpdateXML(args[3], args[5].Replace("\"", "").Split(","), Convert.ToInt32(args[4]));
                }
                else
                {
                    Console.WriteLine($"Unknown metadata type: {type}");
                }
            }
            else
            {
                Console.WriteLine($"Unknown metadata subcommand: {subcommand}");
                Console.WriteLine("Use 'GM3P.exe help metadata' for usage");
            }


        }

        static void HandleClear(string[] args)
        {
            string target = args.Length > 1 ? args[1] : "runningCache";
            _orchestrator!.Clear(target);
        }
        static async Task HandlePlay(string[] regArgs, string[] opArg)
        {
            string? game = regArgs.Length > 1 ? regArgs[1] : null;
            string? version = regArgs.Length > 2 ? regArgs[2] : null;
            string? modName = null;
            string? inputList = null;
            for (int i = 0; i < opArg.Length; i++)
            {
                if (opArg[i].StartsWith("--mods "))
                {
                    modName = opArg[i].Replace("--mods ", "");
                }
                if (opArg[i].StartsWith("--inputList "))
                {
                    inputList = opArg[i].Replace("--inputList ", "");
                    inputList = inputList.Replace("\"", "");
                }
            }
            await _orchestrator!.ExecutePlay(game, version, modName, inputList);
        }
        static async Task HandleInstall(string[] regArgs, string[] opArg)
        {
            string modName = "vanilla";
            string? gamePath = regArgs.Length > 1 ? regArgs[1] : null;
            string? game = regArgs.Length > 2 ? regArgs[2] : null;
            string? version = "1.0.0";
            for (int i = 0; i < opArg.Length; i++)
            {
                if (opArg[i].StartsWith("--modName "))
                {
                    modName = opArg[i].Replace("--modName ", "");
                }
                if (opArg[i].StartsWith("--version "))
                {
                    version = opArg[i].Replace("--version ", "");
                }
            }
            await _orchestrator!.ExecuteInstall(modName, gamePath, game, version);
        }
        static async Task RunAppVersion()
        {
            //Console.WriteLine("Read the README for Operating Instructions\n");

            Console.WriteLine("If you want to use the classic console version, enter \"console\" or leave blank.\nIf you want to enter the menu, enter \"menu\". Otherwise enter in a command\n");
            var iknowwhatwearegonnadotodayferb = Console.ReadLine();
            if (iknowwhatwearegonnadotodayferb == "menu")
            {
                Console.WriteLine("Menu is not implemented yet, please use the console version");
                await RunConsoleApp();
            }
            else if (iknowwhatwearegonnadotodayferb != "console" && !string.IsNullOrWhiteSpace(iknowwhatwearegonnadotodayferb))
            {
                var args = Regex.Split(iknowwhatwearegonnadotodayferb, "(?:^| )(\"(?:[^\"]+|\"\")*\"|[^ ]*)");
                args = args.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToArray();
                args = args.Select(arg => arg.Trim('"')).ToArray();
                Console.WriteLine("\nargs: \n");
                for (int i = 0; i < args.Length; i++)
                {
                    Console.WriteLine(args[i]);
                }
                Console.WriteLine("");
                await RunCommand(args);
                if (args != null && args.Length > 0 && args[0].ToLower() != "exit")
                {
                    await RunAppVersion();
                }
            }
            else
            {
                await RunConsoleApp();
            }
        }

        static async Task RunConsoleApp()
        {

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
            if (args.Length > 2)
            {
                ShowCommandHelp(args[1], args[2]);
            }
            else if (args.Length > 1)
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
                Console.WriteLine("  config     - Update configuration");
                Console.WriteLine("  play       - Launch game with or without modpack");
                Console.WriteLine("  import     - Import game or mod into the mod manager");
                Console.WriteLine("  metadata   - Manipulate Deltamod-style metadata for modpacks");
                Console.WriteLine("\nUse 'GM3P.exe help [command]' for detailed help");
            }
        }

        static void ShowCommandHelp(string command, string? subcommand=null)
        {
            switch (command.ToLower())
            {
                case "config":
                    Console.WriteLine("\nConfig Command:");
                    Console.WriteLine("  Save and update configuration settings");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe config update c.[setting] [Value] save? [configPath?]");
                    Console.WriteLine("\nSettings:");
                    Console.WriteLine("  c.vanillapath          - Path to vanilla game or data.win");
                    Console.WriteLine("  c.outputpath           - Base output directory. Default: ./output");
                    Console.WriteLine("  c.deltapatcherpath     - Path to xDelta executable. Default: ./tools/xdelta3.exe");
                    Console.WriteLine("  c.modtoolpath          - Path to mod tool executable (e.g. UTMT). Default: ./tools/UTMTCLI/UndertaleModCli.exe");
                    Console.WriteLine("  c.gameengine           - Game engine type (e.g. GM for GameMaker). Currently unused");
                    Console.WriteLine("  c.modamount            - Number of mods to patch/compare");
                    Console.WriteLine("  c.chapteramount        - Number of chapters to patch. Default: 1)");
                    Console.WriteLine("  c.combined             - Whether mods were combined (true/false). Default: false");
                    Console.WriteLine("  c.enablefastcombiner   - Whether to enable fast combiner (true/false), must be false for room combining. Default: false");
                    Console.WriteLine("  c.combinertool         - Tool to use for combining mods. Default: GM3P 0");
                    Console.WriteLine("  c.verboselogging       - Whether verbose logging is enabled (true/false). Default: false");
                    Console.WriteLine("  c.win                  - Whether to include the data.win in the result (true/false). Default: true");
                    Console.WriteLine("  c.mergemethod          - Method to use for merging mods. Default: both");
                    Console.WriteLine("  c.utmtversion          - Version of UTMT to use. Default: 8");
                    Console.WriteLine("  c.treatg3mpatchaszip   - Whether to treat G3MPatch files as ZIP folders (true/false). Default: false");
                    Console.WriteLine("  c.cacheenabled         - Whether to enable export cache (true/false). Default: false");
                    Console.WriteLine("  c.cachespritesenabled  - Whether to cache sprites in export cache (true/false). Default: false");
                    Console.WriteLine("  c.exportcachecapmb     - Export cache size cap in MB. Default: 1024");
                    Console.WriteLine("  c.xdeltaconcurrency    - Number of concurrent xDelta processes. Default: 3");
                    break;
                case "masspatch":
                    Console.WriteLine("\nMassPatch Command:");
                    Console.WriteLine("  Patches multiple data.win files with mods");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe massPatch [VanillaPath] [ModAmount] [PatchPaths] --config? <ConfigPath> --relative? <releativePath>");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  VanillaPath - Path to vanilla game or data.win");
                    Console.WriteLine("  ModAmount   - Number of mods to patch");
                    Console.WriteLine("  PatchPaths  - Mod file paths (:: for chapters, , for mods)");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  config      - Optional config JSON");
                    Console.WriteLine("  relative    - Relative path for patch files");
                    break;

                case "compare":
                    Console.WriteLine("\nCompare Command:");
                    Console.WriteLine("  Compares and combines mod objects");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe compare [ModAmount] --noDump? --noImport? --config? <ConfigPath>");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  ModAmount  - Number of mods");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  noDump     - Do not dump objects");
                    Console.WriteLine("  noImport   - Do not import objects");
                    Console.WriteLine("  config     - Load optional configuration json");
                    break;

                case "result":
                    Console.WriteLine("\nResult Command:");
                    Console.WriteLine("  Creates final modpack files");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe result [ModName] --notCombined? <ModAmount> --config? <ConfigPath> --lose");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  ModName    - Name for the modpack");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  notCombined   - mods weren't combined");
                    Console.WriteLine("  ModAmount     - Number of mods");
                    Console.WriteLine("  config        - Optional config JSON");
                    Console.WriteLine("  lose          - Will not include the data.win in the result");
                    break;
                case "import":
                    Console.WriteLine("\nImport Command:");
                    Console.WriteLine("  Imports mods and instances into the mod manager");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe import [GamePath] [Game] --modName? <ModName> --version? <Version>");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  GamePath  - Path to the game directory");
                    Console.WriteLine("  Game      - Game identifier");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  ModName   - Name of the mod to import (default: vanilla)");
                    Console.WriteLine("  version   - Game version (optional, but recommended)");
                    break;
                case "play":
                    Console.WriteLine("\nPlay Command:");
                    Console.WriteLine("  Launches the game with the specified modpack");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe play [Game] [Version] --mods <ModName> --inputList <InputListPath>");
                    Console.WriteLine("\nArguments:");
                    Console.WriteLine("  Game       - Game identifier");
                    Console.WriteLine("  Version    - Game version");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  mods       - Name of the modpack to play (optional)");
                    Console.WriteLine("  inputList  - Path to input list file (optional)");
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
                case "metadata":
                    ShowMetadataHelp(subcommand);
                    break;

                default:
                    Console.WriteLine($"No help available for command: {command}");
                    break;
            }
        }
        static void ShowMetadataHelp(string? subcommand = null)
        {
            switch (subcommand?.ToLower())
            {
                case "parse":
                    Console.WriteLine("Usage: GM3P.exe metadata parse [xml|json] [Path]");
                    Console.WriteLine("  Parses and prints metadata from the specified XML or JSON file.");
                    Console.WriteLine("  For XML, if the path is 'mem' then it will then instead print what is loaded");
                    break;
                case "export":
                    Console.WriteLine("Usage: GM3P.exe metadata export [xml|json] [Path] [result (xml only)]");
                    Console.WriteLine("  Exports metadata to the specified XML or JSON file.");
                    Console.WriteLine("  For XML, the 'result' argument specifies a modpack made from the 'result' command (e.g., 'MyMod'). In which a modding.xml will be built based on that modpack");
                    break;
                case "update":
                    Console.WriteLine("Usage: GM3P.exe metadata update [xml|json]");
                    Console.WriteLine("  Updates metadata fields in the loaded XML or JSON.");
                    Console.WriteLine("  Use 'GM3P.exe help metadata update-json' or 'GM3P.exe help metadata update-xml' for more details.");
                    break;
                case "update-json":
                    Console.WriteLine("Usage: GM3P.exe metadata update json [Key] [Value]");
                    Console.WriteLine("  Updates metadata fields in the JSON file.");
                    Console.WriteLine("Keys:");
                    Console.WriteLine("  name, author, version, description, url, game, packageID, color (R,G,B), neededFiles (file,checksum), UTMTVersion, deltaruneTargetVersion");
                    Console.WriteLine("  Example: GM3P.exe metadata update json name \"My Modpack\"");
                    Console.WriteLine("  For what each key does, see https://github.com/deltamodders/modding-standard");
                    break;
                case "update-xml":
                    Console.WriteLine("Usage: GM3P.exe metadata update xml [Action] [Entry Number] [Entry]");
                    Console.WriteLine("  Updates metadata fields in the XML file.");
                    Console.WriteLine("Actions:");
                    Console.WriteLine("  add     - Adds an entry to the modding.xml. [Entry Number] is ingored for this.");
                    Console.WriteLine("  change  - changes an existing entry in the modding.xml.");
                    Console.WriteLine("  remove  - Removes an existing entry. [Entry] is ingored for this");
                    Console.WriteLine("  Example: GM3P.exe metadata update xml change 3 \"xdelta,./chapter3fix.xdelta,./chapter3_windows/data.win\"");
                    break;
                default:
                    Console.WriteLine("Metadata Command:");
                    Console.WriteLine("  Manages modpack metadata in Deltamod format");
                    Console.WriteLine("\nUsage:");
                    Console.WriteLine("  GM3P.exe metadata [parse|export|update] [xml|json] [Path] [Key] [Value]");
                    Console.WriteLine("\nSubcommands:");
                    Console.WriteLine("  parse   - Load and Print metadata from XML or JSON");
                    Console.WriteLine("  export  - Export metadata to XML or JSON");
                    Console.WriteLine("  update  - Update metadata fields in JSON");
                    break;
            }
        }
    }
}