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
namespace GM3P
{
    internal class Main
    {
        /// <summary>
        /// The path to the vanilla game
        /// </Summary>
        public static string? vanilla2 { get; set; }
        public static string vanilla = vanilla2.Replace("\"", "");
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
        public static string DeltaPatcher2 {get; set;}
        public static string DeltaPatcher = DeltaPatcher2.Replace("\"", "");
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
            Directory.CreateDirectory(Main.output + @"\xDeltaCombiner");
            for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
            {

                Directory.CreateDirectory(Main.output + "\\xDeltaCombiner\\" + modNumber);
                Directory.CreateDirectory(Main.output + "\\xDeltaCombiner\\" + modNumber + "\\Objects");
                File.Copy(Main.@vanilla, Main.output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", true);

            }
        ;
        }
        public static string[] xDeltaFile2 = new string[(Main.modAmount + 2)];
        public static string[] xDeltaFile = new String[(Main.modAmount + 2)];
        public static string[] xDeltaFileLinux2 = new string[(Main.modAmount + 2)];
        public static string[] xDeltaFileLinux = new string[(Main.modAmount + 2)];
        public static void getModPaths()
        {

            
            for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
            {
                xDeltaFile2[modNumber] = Console.ReadLine();

            }
    ;
        }
        /// <summary>
        /// Patches the xDeltas into a bunch of data.wins
        /// </summary>
        public static void massPatch()
        {
            for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
            {
                File.WriteAllText(Main.output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
                xDeltaFile[modNumber] = xDeltaFile2[modNumber].Replace("\"", "");
                xDeltaFileLinux2[modNumber] = xDeltaFile[modNumber].Replace("\\", "/");
                xDeltaFileLinux[modNumber] = xDeltaFileLinux2[modNumber].Replace("C:", "c");
                using (var bashProc = new Process())
                {
                    bashProc.StartInfo.FileName = Main.DeltaPatcher;
                    bashProc.StartInfo.Arguments = "-v -d -f -s " + Main.output + "\\xDeltaCombiner\\0\\data.win" + " \"" + xDeltaFile[modNumber] + "\" \"" + Main.output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + "\" ";
                    bashProc.StartInfo.CreateNoWindow = false;
                    bashProc.Start();
                    bashProc.WaitForExit();
                }
            }
        }
        public static List<string> modifedAssets = new List<string> { "Asset Name                       Hash (SHA1 in Base64)" };
        public static void modifiedListCreate() {
            if (!File.Exists(Main.output + "\\xDeltaCombiner\\1\\modifedAssets.txt"))
            {
                File.Create(Main.output + "\\xDeltaCombiner\\1\\modifedAssets.txt").Close();
            }
            
        }
}
}
