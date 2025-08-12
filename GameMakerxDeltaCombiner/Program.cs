// See https://aka.ms/new-console-template for more information
using GM3P;
using GM3P.modNumbers;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GM3P.Program;

class Program
{
    static void Main(string[] args = null)
    {
        //Store version as a double and print full version #
        double Version = 0.5;
        Console.WriteLine("GM3P v" + Version + ".1");
        
        //Create logging file and start logging
        string startTime = DateTime.Now.ToString("yy") + DateTime.Now.ToString("MM") + DateTime.Now.ToString("dd") + DateTime.Now.ToString("HH") + DateTime.Now.ToString("mm") + DateTime.Now.ToString("zz");
        GM3P.Main.output = GM3P.Main.pwd + "/output";
        Directory.CreateDirectory(GM3P.Main.@output + "/Cache");
        Directory.CreateDirectory(GM3P.Main.@output + "/Cache/Logs/");
        Directory.CreateDirectory(GM3P.Main.@output + "/Cache/running");
        File.Create(GM3P.Main.@output + "/Cache/Logs/" + startTime + ".txt").Close();
        using (var cc = new ConsoleCopy(GM3P.Main.@output + "/Cache/Logs/" + startTime + ".txt"))
        {
            //CLI parser
            if (args != null)
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Starting Console App...");
                    consoleApp();

                }
                var command = args[0];
                switch (command)
                {
                    case "massPatch":
                        GM3P.Main.vanilla2 = args[1];

                        GM3P.Main.output = GM3P.Main.pwd + "/output";
                        if (args.Length > 5)
                        {

                                GM3P.Main.output = args[5];

                        }
                        GM3P.Main.DeltaPatcher = GM3P.Main.pwd + "/xdelta3-3.0.11-x86_64.exe";
                        if (OperatingSystem.IsLinux())
                        {
                            GM3P.Main.DeltaPatcher = "xdelta3 ";
                        }
                        GM3P.Main.gameEngine = args[2];
                        GM3P.Main.modAmount = Convert.ToInt32(args[3]);
                        GM3P.Main.CreateCombinerDirectories();
                        GM3P.Main.CopyVanilla();
                        GM3P.Main.massPatch(args[4].Split("::").ToArray());
                        break;
                    case "compare":
                        GM3P.Main.output = GM3P.Main.pwd + "/output";
                        GM3P.Main.modAmount = Convert.ToInt32(args[1]);
                        if (args.Length > 2)
                        {
                            GM3P.Main.modTool = GM3P.Main.pwd + "/UTMTCLI/UndertaleModCli.exe";
                            if (OperatingSystem.IsLinux())
                            {
                                GM3P.Main.modTool = "dotnet \"" + GM3P.Main.@pwd + "/UTMTCLI/UndertaleModCli.dll\" ";
                            }
                            if (args.Length > 4)
                            {
                                GM3P.Main.output = args[4];
                            }
                            if (args[2] == "true")
                            {
                                GM3P.Main.dump();
                            }
                        }
                        GM3P.Main.modifiedListCreate();
                        GM3P.Main.CompareCombine();
                        GM3P.Main.loadCachedNumbers();
                        for (int ch = 0; ch < GM3P.Main.chapterAmount; ch++)
                        {
                            File.WriteAllLines(GM3P.Main.@output + $"/xDeltaCombiner/{ch}/1/modifiedAssets.txt", GM3P.Main.modifiedAssets);
                        }
                        if (args.Length > 3)
                        {
                            if (args[3] == "true")
                            {
                                GM3P.Main.HandleNewObjects();    
                                GM3P.Main.importWithNewObjects(); 
                            }
                        }
                        break;
                    case "result":

                        GM3P.Main.DeltaPatcher = GM3P.Main.pwd + "/xdelta3-3.0.11-x86_64.exe";
                        if (OperatingSystem.IsLinux())
                        {
                            GM3P.Main.DeltaPatcher = "xdelta3 ";
                        }
                        if (args.Length > 2)
                        {
                            GM3P.Main.combined = Convert.ToBoolean(args[2]);
                            if (args.Length > 3)
                            {
                                GM3P.Main.modAmount = Convert.ToInt32(args[3]);
                            }
                            if (args.Length > 4)
                            {
                                GM3P.Main.output = args[4];
                            }
                        }
                        GM3P.Main.result(args[1]);
                        break;
                    case "cache":
                        break;
                    case "console":
                        Console.WriteLine("Starting Console App...");
                        consoleApp();
                        break;
                    case "clear":
                        if (args.Length > 2)
                        {
                            GM3P.Main.output = args[2];
                        }
                        if (args.Length > 1)
                        {
                            GM3P.Main.clear(args[1]);
                        }
                        if (args.Length == 1) 
                        {
                            GM3P.Main.clear();
                        }
                        break;
                    case null:
                        consoleApp();
                        break;
                    case "help":
                        if (args.Length > 1)
                        {
                            string commandHelp = args[1];
                            if (commandHelp == "massPatch")
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Makes a bunch of patched, single-mod data.win files quickly.");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Command Santax:   GM3P.exe massPatch [Vanilla Copy] [Game Engine] [Amount of Mods] [Mod File Paths] [(optional) Output Folder]");
                                Console.WriteLine(" ");
                                Console.WriteLine("Example:          GM3P.exe massPatch \"C:/Program Files(x86)/Steam/steamapps/common/DELTARUNE/chapter3_windows/data.win\" GM 2 \",,F:/Downloads/a.xDelta,F:/Downloads/b.csx\" \"C:/Undertale Mods\"");
                                Console.WriteLine(" ");
                                Console.WriteLine("Args: ");
                                Console.WriteLine(" ");
                                Console.WriteLine("[Vanilla Copy]                The path of either the folder containing the unmodified data.win(s), or a data.win itself. For Example, if you have a Deltarune installation at the root of C:, acceptable arguments would be \"C:\\DELTARUNE\", \"C:\\DELTARUNE\\chapter3_windows\", or \"C:\\DELTARUNE\\chapter3_windows\\data.win\"");
                                Console.WriteLine("[Game Engine]                 Currently Unused, but may be used in the far future when this tool gets ported for use with other game engines, \"GM\" is for GameMaker games.");
                                Console.WriteLine("[Amount of Mods]              The amount of mods to patch. If you are doing multi-chapter patching, this would be the most amount of mods you want to patch for any chapter you are patching.");
                                Console.WriteLine("[Mod File Paths]              The Mod File Paths arg must be encased with a double quote (\"), but the individual paths cannot be encased with, nor contain, a double quote. Mod File Paths are 2D arrays first delimited by double collins (::) for chapters, then are delimited by commas (,) for the mod paths for the chapter. Paths entered before the second comma delimiter are ingored.");
                                Console.WriteLine("[Mod File Paths](cont.)       Examples of accepable inputs for this argument: \",,F:\\UTDR Mods\\mod1.xdelta,F:\\UTDR Mods\\mod2.xdelta\" and \",,F:\\UTDR Mods\\mod1-root.csx,::,,F:\\UTDR Mods\\mod1-ch1.win,F:\\UTDR Mods\\mod2-ch1.xdelta::,,F:\\UTDR Mods\\mod1-ch2.xdelta,F:\\UTDR Mods\\mod2-ch2.csx\"");
                                Console.WriteLine("[(optional) Output Folder]    Where the output folder would be (as specified in README section 2.2), the default is under the GM3P executable folder");
                            }
                            if (commandHelp == "clear")
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Clears the xDeltaCombiner folders for reuse on future uses. Optionally clears other stuff GM3P modifies or writes to");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Command Santax:   GM3P.exe clear [(optional) what to clear] [(optional) Output Folder]");
                                Console.WriteLine(" ");
                                Console.WriteLine("Example:          GM3P.exe clear modpacks \"C:/Undertale Mods\"");
                                Console.WriteLine(" ");
                                Console.WriteLine("Args: ");
                                Console.WriteLine(" ");
                                Console.WriteLine("[(optional) what to clear]    Acceptable inputs: runningCache (deletes \"xDeltaCombiner\" and \"Cache/running\" folders; default), modpacks (deletes \"result\" folder), cache (deletes \"Cache\" folder), and output (deletes everything that's modifiable by GM3P)");
                                Console.WriteLine("[(optional) Output Folder]    Where the output folder would be (as specified in README section 2.2), the default is under the GM3P executable folder");
                            }
                            if (commandHelp == "result")
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Makes .xDelta(s) and .win(s) for the resulting modpack.");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Command Santax:   GM3P.exe result [modpack or modset name] [whether or not compare was called before] [amount of mods] [(optional) output folder]");
                                Console.WriteLine(" ");
                                Console.WriteLine("Example:          GM3P.exe \"My Modset\" result true 4 \"C:/Undertale Mods\"");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Args: ");
                                Console.WriteLine(" ");
                                Console.WriteLine("[modpack or modset name]                     The name you would wish to call the modpack or modset.");
                                Console.WriteLine("[whether or not compare was called before]   Whether or not compare was called before this. Enter as a boolean.");
                                Console.WriteLine("[Amount of Mods]                             Required if the previous arg is \"false\", otherwise ingored. The amount of mods to patch. If you are doing multi-chapter patching, this would be the most amount of mods you want to patch for any chapter you are patching.");
                                Console.WriteLine("[(optional) Output Folder]                   Where the output folder would be (as specified in README section 2.2), the default is under the GM3P executable folder");
                            }
                            if (commandHelp == "console")
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Launches console app.");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Command Santax:   GM3P.exe console");
                                Console.WriteLine(" ");
                                Console.WriteLine("Example:          GM3P.exe console");
                                Console.WriteLine(" ");
                                Console.WriteLine("Note: The console app can also be launched if no command is provided. ");
                            }
                            if (commandHelp == "compare")
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Compares and combine GM objects. Dumping and importing optional.");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine(" ");
                                Console.WriteLine("Command Santax:   GM3P.exe compare [Amount of Mods] [(optional) Dump] [(optional) Import] [(optional) Output Folder]");
                                Console.WriteLine(" ");
                                Console.WriteLine("Example:          GM3P.exe compare 2 false true \"C:/Undertale Mods\"");
                                Console.WriteLine(" ");
                                Console.WriteLine("Note: Can only be successfully called if massPatch was called before.");
                                Console.WriteLine(" ");
                                Console.WriteLine("Args: ");
                                Console.WriteLine(" "); 
                                Console.WriteLine("[Amount of Mods]              The amount of mods to patch. If you are doing multi-chapter patching, this would be the most amount of mods you want to patch for any chapter you are patching.");
                                Console.WriteLine("[(optional) Dump]             whether or not to automatically dump objects. For those wanting to manually dump and for toolmakers who want to implement their own way to dump objects. Default is true.");
                                Console.WriteLine("[(optional) Import]           whether or not to automatically import objects. For those wanting to manually import objects and for toolmakers who want to implement their own way to import. ");
                                Console.WriteLine("[(optional) Output Folder]    Where the output folder would be (as specified in README section 2.2), the default is under the GM3P executable folder. Automatic dumping and importing is currently not supported with a custom output folder.");
                            }
                        }
                        if (args.Length == 1)
                        {
                            Console.WriteLine("Avalible commands:\nhelp        Display uses of the commands and exits. (use \"GM3P.exe help *command*\")\nmassPatch   Patches a lot of data.win files with a single mod each\nconsole     launches console app\nclear       Clears the xDeltaCombiner folder for future use.\ncompare     Compares modded GM Objects to vanilla and puts changes in a list. Dumping and Importing optional.\nresult      Name and copy modpack to a folder\n");
                        }
                        break;
                    default:
                        Console.WriteLine("Invalid command");
                        break;
                }
            }
            else
            {
                consoleApp();
            }
            //Console App
            static void consoleApp()
            {
                Console.WriteLine("Read the README for Operating Instructions\n");
                Console.WriteLine("Insert the path to the vanilla data.win, or type \"skip\" if want skip to compare and combine:");
                GM3P.Main.vanilla2 = Console.ReadLine().Replace("\"", "");
                GM3P.Main.output = GM3P.Main.@pwd + "/output";
                GM3P.Main.DeltaPatcher = GM3P.Main.@pwd + "/xdelta3-3.0.11-x86_64.exe";
                if (OperatingSystem.IsLinux())
                {
                    GM3P.Main.DeltaPatcher = "xdelta3 ";
                }

                Console.WriteLine("Type however many mods you want to patch (If you are patching multiple chapters, this would be the amount of mods for a single chapter): ");
                GM3P.Main.modAmount = Convert.ToInt32(Console.ReadLine());

                if (GM3P.Main.vanilla2 != "skip")
                {
                    GM3P.Main.CreateCombinerDirectories();
                    GM3P.Main.CopyVanilla();
                    Console.WriteLine("Now Enter in the patches, one at a time (If you are doing multi-chapter patching, do the mods for the root first): ");
                    GM3P.Main.massPatch();
                }
                else
                {
                    // Try to load cached chapter amount
                    GM3P.Main.loadCachedNumbers();
                }
                    Console.WriteLine("Enter in the Mod Tool (e.g. UnderTaleModTool for GameMaker Games). If you want to use the included tool, just hit enter. If you want to manually dump and import enter \"skip\"");
                    Console.WriteLine("If you don't want to combine patches and just wanted to apply them, you may enter \"noCombine\"");
                    GM3P.Main.modTool = Console.ReadLine();
                if (GM3P.Main.modTool != "noCombine")
                {
                    if (GM3P.Main.modTool == null || GM3P.Main.modTool == "")
                    {
                        GM3P.Main.modTool = GM3P.Main.@pwd + "/UTMTCLI/UndertaleModCli.exe";
                        if (OperatingSystem.IsLinux())
                        {
                            GM3P.Main.modTool = "dotnet '" + GM3P.Main.@pwd + "/UTMTCLI/UndertaleModCli.dll' ";
                        }
                    }
                    if (GM3P.Main.modTool != "skip")
                    {
                        GM3P.Main.dump();
                        Console.WriteLine("The dumping process(es) are finished. Hit Enter to Continue.");
                    }
                    else
                    {
                        Console.WriteLine("In order to dump manually, load up the data.win in each of the /xDeltaCombiner/ subfolders into the GUI version of UTMT and run the script ExportAllCode.csx. Select \"C:/xDeltaCombiner/*currentsubfolder*/Objects/\" as your destination. Once finished, exit without saving.");
                        Console.WriteLine("Press Enter when done with the above instructions");
                    }
                    Console.ReadLine();
                    GM3P.Main.modifiedListCreate();
                    GM3P.Main.CompareCombine();

                    File.WriteAllLines(GM3P.Main.@output + "/xDeltaCombiner/0/1/modifiedAssets.txt", GM3P.Main.modifiedAssets);
                    Console.WriteLine("Comparing is done. Hit Enter to Continue.");
                    Console.ReadLine();
                    GM3P.Main.HandleNewObjects();
                    GM3P.Main.importWithNewObjects();
                }
                Console.WriteLine("To save your modpack or modset, name it: ");
                GM3P.Main.result(Console.ReadLine());
                Console.WriteLine("Press Enter To Clean up (Will delete output/xDeltaCombiner) and exit");
                Console.ReadLine();
                GM3P.Main.clear();
                Environment.Exit(1);
            }

        }
    }
}