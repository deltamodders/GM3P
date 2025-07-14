using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GM3P.modNumbers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
namespace GM3P
{
    internal class Main
    {
        /// <summary>
        /// The path to the vanilla game
        /// </Summary>
        public static string? vanilla2 { get;  set; }
        //public static string vanilla = Main.vanilla2.Replace("\"", "");
        /// <summary>
        /// Current working directory
        /// </summary>
        public static string pwd = Convert.ToString(Directory.GetParent(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
        /// <summary>
        /// Output folder
        /// </summary>
        public static string output { get; set; }
        /// <summary>
        /// path to an xDelta patcher, e.g. xDelta3 or Deltapatcher
        /// </summary>
        public static string DeltaPatcher {get; set;}
        /// <summary>
        /// Amount of mods to merge
        /// </summary>
        public static int modAmount { get; set; }
        /// <summary>
        /// Path to the modTool for Dumping
        /// </summary>
        public static string modTool { get; set; }
        public static void CreateCombinerDirectories()
        {


            Directory.CreateDirectory(Main.@output + @"\xDeltaCombiner");
            for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
            {

                Directory.CreateDirectory(Main.@output + "\\xDeltaCombiner\\" + modNumber);
                Directory.CreateDirectory(Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects");
                File.Copy(Main.@vanilla2, Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", true);

            }
        ;
        }
        public static string[] xDeltaFile { get; set; }
        /// <summary>
        /// Patches the xDeltas into a bunch of data.wins
        /// </summary>
        public static void massPatch(string[] filepath = null)
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
                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    xDeltaFile[modNumber] = filepath[modNumber].Replace("\"", "");

                }
            }
                for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
                {
                    //Check if the mod is a UTMT script. If so, patch it.
                    if (Path.GetExtension(xDeltaFile[modNumber]) == ".csx")
                    {
                        using (var modToolProc = new Process())
                        {
                            modToolProc.StartInfo.FileName = Main.@modTool;
                            modToolProc.StartInfo.Arguments = "load " + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win " + "--verbose --output " + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + " --scripts " + xDeltaFile[modNumber];
                            modToolProc.StartInfo.CreateNoWindow = false;
                            modToolProc.Start();
                            modToolProc.WaitForExit();
                        }
                    }
                    //If it's a full data.win, copy the file
                    else if (Path.GetExtension(xDeltaFile[modNumber]) == ".win")
                    {
                    File.Copy(xDeltaFile[modNumber], Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + "\" ");
                    }
                    //Otherwise, patch the xDelta
                    else
                    {
                        File.WriteAllText(Main.@output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
                        using (var bashProc = new Process())
                        {
                            bashProc.StartInfo.FileName = Main.DeltaPatcher;
                            bashProc.StartInfo.Arguments = "-v -d -f -s " + Main.@output + "\\xDeltaCombiner\\0\\data.win" + " \"" + xDeltaFile[modNumber] + "\" \"" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + "\" ";
                            bashProc.StartInfo.CreateNoWindow = false;
                            bashProc.Start();
                            bashProc.WaitForExit();
                        }
                    }
                }
        }
        public static List<string> modifedAssets = new List<string> { "Asset Name                       Hash (SHA1 in Base64)" };
        public static void modifiedListCreate() {
            if (!File.Exists(Main.@output + "\\xDeltaCombiner\\1\\modifedAssets.txt"))
            {
                File.Create(Main.@output + "\\xDeltaCombiner\\1\\modifedAssets.txt").Close();
            }

        }
        /// <summary>
        /// Dumps objects from mod
        /// </summary>
        public static void dump()
        {
            for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
            {
                Directory.CreateDirectory(Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\CodeEntries");
                File.WriteAllText(Main.@output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
                if (modNumber != 1)
                {
                    using (var modToolProc = new Process())
                    {
                        modToolProc.StartInfo.FileName = Main.@modTool;
                        modToolProc.StartInfo.Arguments = "load " + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win " + "--verbose --output " + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + " --scripts " + Main.@pwd + "\\UTMTCLI\\Scripts\\ExportAllTexturesGrouped.csx --scripts " + Main.pwd + "\\UTMTCLI\\Scripts\\ExportAllCode.csx --scripts " + Main.@pwd + "\\UTMTCLI\\Scripts\\ExportAssetOrder.csx";
                        modToolProc.StartInfo.CreateNoWindow = false;
                        modToolProc.Start();
                        modToolProc.WaitForExit();
                    }
                }
            }
        }
}
}
