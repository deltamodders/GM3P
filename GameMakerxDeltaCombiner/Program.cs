// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Immutable;
using System.Collections.Generic;
using GM3P;

double Version = 0.4;
Console.WriteLine("GM3P v" + Version + ".0-alpha1");
Console.WriteLine("Insert the path to the vanilla data.win, or type \"skip\" if you just want to compare and combine:");
Main.vanilla2 = Console.ReadLine().Replace("\"","");
Main.output = Main.pwd + "\\output";
Main.DeltaPatcher = Main.pwd + "\\xdelta3-3.0.11-x86_64.exe";
Console.WriteLine("Type however many mods you want to patch: ");
Main.modAmount = Convert.ToInt32(Console.ReadLine());
if (Main.vanilla2 != "skip")
{
    //Console.WriteLine("Enter in the path of a xdelta3 executable: ");

    Main.CreateCombinerDirectories();
    Main.CopyVanilla();
    Console.WriteLine("Now Enter in the xDeltas, one at a time: ");
    Main.massPatch();
}
Console.WriteLine("Enter in the Mod Tool (e.g. UnderTaleModTool for GameMaker Games). If you want to use the included tool, just hit enter. If you want to manually dump and import enter \"skip\"");
Console.WriteLine("If you don't want to combine patches and just wanted to apply them, you may exit the terminal now");
Main.modTool = Console.ReadLine();
if (Main.modTool == null || Main.modTool == "")
{
    Main.modTool = Main.pwd + "\\UTMTCLI\\UndertaleModCli.exe";
}
if (Main.modTool != "skip")
{
    Main.dump();
    Console.WriteLine("The dumping process(es) are finished");
}
if (Main.modTool == "skip")
{
    Console.WriteLine("In order to dump manually, load up the data.win in each of the \\xDeltaCombiner\\ subfolders into the GUI version of UTMT and run the script ExportAllCode.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\\\" as your destination. Once finished, exit without saving.");
    Console.WriteLine("Press Enter when done with the above instructions");
}
Console.ReadLine();
Main.modifiedListCreate();
Main.CompareCombine();

File.WriteAllLines(Main.@output + "\\xDeltaCombiner\\1\\modifedAssets.txt", Main.modifedAssets);
Console.WriteLine("Comparing is done. Hit Enter to Continue.");
Console.ReadLine();
Main.import();
Console.WriteLine("To save your modpack, name it: ");
string modname = Console.ReadLine();
if (modname != null && modname != "")
{
    Directory.CreateDirectory(Main.@output + "\\result\\" + modname + "\\");
    using (var bashProc = new Process())
    {
        bashProc.StartInfo.FileName = Main.DeltaPatcher;
        bashProc.StartInfo.Arguments = "-v -e -f -s " + Main.@output + "\\xDeltaCombiner\\0\\data.win" + " \"" + Main.@output + "\\xDeltaCombiner\\1\\data.win" + "\" \"" + Main.@output + "\\result\\"+modname+"\\"+modname+".xdelta\"";
        bashProc.StartInfo.CreateNoWindow = false;
        bashProc.Start();
        bashProc.WaitForExit();
    }
    File.Copy(Main.@output + "\\xDeltaCombiner\\1\\data.win", Main.@output + "\\result\\" + modname + "\\data.win");
    File.Copy(Main.@output + "\\xDeltaCombiner\\1\\modifedAssets.txt", Main.@output + "\\result\\" + modname + "\\modifedAssets.txt");
}
Console.WriteLine("Press Enter To Clean up (Will delete output\\xDeltaCombiner) and exit");
Console.ReadLine();
for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
{
    //if (modNumber != 1)
    //{
        Directory.Delete(Main.output + "\\xDeltaCombiner\\" + modNumber, true);
    //}
}