using GM3P.modNumbers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
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

            }
        ;
        }
        /// <summary>
        /// Copy vanilla files as much as needed
        /// </summary>
        public static void CopyVanilla()
        {
            for (int modNumber = 0; modNumber < (Main.modAmount + 2); modNumber++)
            {
                File.Copy(Main.@vanilla2, Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", true);
            }
        }
        public static string[] xDeltaFile { get; set; }
        /// <summary>
        /// The titular function; patches a bunch of mods into data.win files
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
        /// <summary>
        /// The function that's the main draw of GM3P; Compares and Combines objects
        /// </summary>
        public static void CompareCombine()
        {
            int vanillaFileCount = Convert.ToInt32(Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\0\\Objects\\", "*", SearchOption.AllDirectories).Length);
            string[] vanillaFiles = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\0\\Objects\\", "*", SearchOption.AllDirectories);
            string[] vanillaFilesName = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\0\\Objects\\", "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray();
            for (int modNumber = 2; modNumber < (Main.modAmount + 2); modNumber++)
            {
                int modFileCount = Convert.ToInt32(Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories).Length);
                string[] modFiles = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories);
                string[] modFilesName = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray();
                string[] modFileAdditions = modFilesName.Except(vanillaFilesName).ToArray();
                int modFileAdditionsCount = Convert.ToInt32(modFileAdditions?.Length);
                for (int i = 0; i < modFileCount; i++)
                {
                    int k = 0;
                    string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFiles[i]))?.Name + "\\" + Directory.GetParent(modFiles[i])?.Name;
                    for (int j = 0; j < vanillaFileCount; j++)
                    {
                        //For Debugging Copying Files
                        //if (i == 0 && k == 0)
                        //{
                        //    for (int k2 = 0; k2 < vanillaFileCount; k2++)
                        //    {
                        //        string? vanillaFileDir = Directory.GetParent(Path.GetDirectoryName(vanillaFiles[k2])).Name + "\\" + Directory.GetParent(vanillaFiles[k2]).Name;
                        //        if (vanillaFileDir != "Objects\\CodeEntries")
                        //        {
                        //            if (vanillaFileDir == ("0\\Objects"))
                        //            {

                        //                File.Copy(Path.GetDirectoryName(vanillaFiles[k2]) + "\\" + Path.GetFileName(vanillaFiles[k2]), Main.output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(vanillaFiles[k2]), true);
                        //            }
                        //            if (vanillaFileDir != ("0\\Objects"))
                        //            {
                        //                Directory.CreateDirectory(Main.output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                        //                File.Copy(Path.GetDirectoryName(vanillaFiles[k2]) + "\\" + Path.GetFileName(vanillaFiles[k2]), Main.output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[k2]), true);
                        //            }
                        //        }
                        //        Console.WriteLine("Copying" + vanillaFiles[k2]);
                        //    }
                        //    k++;
                        //}
                        if (Path.GetFileName(vanillaFiles[j]) == Path.GetFileName(modFiles[i]) && (modFilesName[i] != "AssetOrder.txt"))
                        {
                            Console.WriteLine("Currently Comparing " + Path.GetFileName(vanillaFiles[j]) + " to " + Path.GetFileName(modFiles[i]));

                            if (vanillaFileCount <= modFileCount)
                            {
                                SHA1 vanillaHashing = new SHA1CryptoServiceProvider();

                                using (FileStream fs = File.OpenRead(vanillaFiles[j]))
                                {
                                    string vanillaHash = Convert.ToBase64String(vanillaHashing.ComputeHash(fs));
                                    SHA1 modHashing = new SHA1CryptoServiceProvider();

                                    using (FileStream fx = File.OpenRead(modFiles[j]))
                                    {
                                        string modHash = Convert.ToBase64String(modHashing.ComputeHash(fx));
                                        Console.WriteLine(modHash);

                                        if (modHash != vanillaHash)
                                        {
                                            Console.WriteLine(vanillaHash);
                                            Console.WriteLine(modFileDir);
                                            if (modFileDir == ("Objects\\CodeEntries"))
                                            {
                                                Console.WriteLine("Copying " + Path.GetFileName(modFiles[i]));
                                                File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\CodeEntries\\" + Path.GetFileName(vanillaFiles[j]), true);
                                                Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                            }
                                            if (modFileDir != ("Objects\\CodeEntries"))
                                            {


                                                if (Path.GetExtension(modFiles[i]) == ".png")
                                                {

                                                    using (Bitmap image1 = new Bitmap(modFiles[i]))
                                                    {

                                                        using (Bitmap image2 = new Bitmap(vanillaFiles[j]))
                                                        {

                                                            [DllImport("msvcrt.dll")]
                                                            static extern int memcmp(IntPtr b1, IntPtr b2, long count);

                                                            static bool CompareMemCmp(Bitmap b1, Bitmap b2)
                                                            {
                                                                if ((b1 == null) != (b2 == null)) return false;
                                                                if (b1.Size != b2.Size) return false;

                                                                var bd1 = b1.LockBits(new Rectangle(new Point(0, 0), b1.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                                                var bd2 = b2.LockBits(new Rectangle(new Point(0, 0), b2.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                                                                try
                                                                {
                                                                    IntPtr bd1scan0 = bd1.Scan0;
                                                                    IntPtr bd2scan0 = bd2.Scan0;

                                                                    int stride = bd1.Stride;
                                                                    int len = stride * b1.Height;

                                                                    return memcmp(bd1scan0, bd2scan0, len) == 0;
                                                                }
                                                                finally
                                                                {
                                                                    b1.UnlockBits(bd1);
                                                                    b2.UnlockBits(bd2);
                                                                }
                                                            }


                                                            if (!CompareMemCmp(image1, image2))
                                                            {
                                                                Directory.CreateDirectory(Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                                                                File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[j]), true);
                                                                Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                                            }
                                                        }
                                                    }


                                                }

                                            }
                                        }


                                    }

                                }
                            }
                            if (vanillaFileCount > modFileCount)
                            {
                                SHA1 modHashing = new SHA1CryptoServiceProvider();

                                using (FileStream fs = File.OpenRead(modFiles[i]))
                                {
                                    string modHash = Convert.ToBase64String(modHashing.ComputeHash(fs));
                                    SHA1 vanillaHashing = new SHA1CryptoServiceProvider();

                                    using (FileStream fx = File.OpenRead(vanillaFiles[i]))
                                    {
                                        try
                                        {
                                            string vanillaHash = Convert.ToBase64String(vanillaHashing.ComputeHash(fx));
                                            Console.WriteLine(modHash);

                                            if (modHash != vanillaHash)
                                            {
                                                Console.WriteLine(vanillaHash);
                                                Console.WriteLine(modFileDir);
                                                if (modFileDir == (modNumber + "\\Objects"))
                                                {

                                                    File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(vanillaFiles[j]), true);
                                                    Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                                }
                                                if (modFileDir != (modNumber + "\\Objects"))
                                                {
                                                    Directory.CreateDirectory(Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                                                    File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[j]), true);
                                                    Main.modifedAssets.Add(modFilesName[i] + "        " + modHash);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                        }

                                    }

                                }
                            }

                        }

                    }

                }
                if (modFileAdditionsCount >= 1)
                {
                    string[] modFileAddtionsDir = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories).Select(Path.GetFullPath).ToArray();
                    for (int i = 0; i < modFileAdditionsCount; i++)
                    {


                        string[] modFilesParent = Directory.GetFiles("" + Main.@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", modFileAdditions[i], SearchOption.AllDirectories);
                        string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFilesParent[0])).Name + "\\" + Directory.GetParent(modFilesParent[0]).Name;

                        if (modFileDir == (modNumber + "\\Objects"))
                        {

                            File.Copy(Path.GetDirectoryName(modFiles[0]) + "\\" + Path.GetFileName(modFilesParent[0]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(modFileAdditions[i]), true);
                            Main.modifedAssets.Add(modFilesName[i] + "        ");
                        }
                        if (modFileDir != (modNumber + "\\Objects"))
                        {
                            Directory.CreateDirectory(Main.output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                            File.Copy(Directory.GetParent(modFilesParent[0]) + "\\" + Path.GetFileName(modFilesParent[0]), Main.@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(modFileAdditions[i]), true);
                            Main.modifedAssets.Add(modFilesName[i] + "        ");
                        }
                        Console.WriteLine("Currently Copying " + Path.GetFileName(modFileAdditions[i]));

                        Console.WriteLine(modFileAdditionsCount);
                    }
                }
                //string GetLine(string fileName, int line)
                //{
                //    using (var sr = new StreamReader(fileName))
                //    {
                //        for (int i = 1; i < line; i++)
                //            sr.ReadLine();
                //        return sr.ReadLine();
                //    }
                //}
                //string assetOrderSeperator = "a bunch of random characters to define this";
                //var vanillaAssetOrder = File.ReadAllLines(Main.output + "\\xDeltaCombiner\\0\\Objects\\AssetOrder.txt").ToList();
                //int vanillaAssetOrderCount = vanillaAssetOrder.Count;
                //int modAssetOrderCount = File.ReadLines(Main.output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\AssetOrder.txt").Count();
                //for (int i = 0; i < vanillaAssetOrderCount; i++)
                //{
                //    Console.WriteLine("Comparing asset order of vanilla to mod # " + (modNumber-1) + ", Line "+i);
                //    if (!File.Exists(Main.output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt")) { 
                //        File.Create(Main.output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt").Close(); 
                //        }
                //    var modAssetOrder = File.ReadAllLines(Main.output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt").ToList();
                //    string vanillaAssetOrderLine = GetLine(Main.output + "\\xDeltaCombiner\\0\\Objects\\AssetOrder.txt", i);
                //    for (int j = 0; j < modAssetOrderCount; j++)
                //    {
                //        string modAssetOrderLine = GetLine(Main.output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\AssetOrder.txt", j);
                //        bool modAssetOrderinvanillaAssetOrder = vanillaAssetOrder.Contains(modAssetOrderLine);
                //        if (modAssetOrderLine == vanillaAssetOrderLine && modAssetOrderLine.StartsWith("@@"))
                //        {
                //            assetOrderSeperator = modAssetOrderLine;
                //        }

                //        if (modAssetOrderLine == vanillaAssetOrderLine && !modAssetOrder.Contains(assetOrderSeperator))
                //        {
                //            if (modNumber == 2)
                //            { modAssetOrder.Add(modAssetOrderLine); }
                //            else
                //            {
                //                modAssetOrder.Insert(j, vanillaAssetOrderLine);
                //            }
                //        }
                //        if (modAssetOrderLine != vanillaAssetOrderLine && !modAssetOrderinvanillaAssetOrder)
                //        {
                //            if (modNumber == 2)
                //            { modAssetOrder.Add(modAssetOrderLine); }
                //            else
                //            {
                //                modAssetOrder.Insert(j, modAssetOrderLine);
                //            }
                //        }
                //    }
                //    File.WriteAllLines(Main.output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt", modAssetOrder);
                //}
            }
        }
}
}
