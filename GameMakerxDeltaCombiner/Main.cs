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
        /// </summary>
        public static string? vanilla2;
        public static string @pwd = @Convert.ToString(Directory.GetParent(Process.GetCurrentProcess().MainModule.FileName));
        public static string? output;
        public static string? DeltaPatcher;
        public static int modAmount;
        /// <summary>
        /// Currently unused except as a CLI arg, but this will be used to determine what Game Engine the game is in in a far future release. Use "GM" is for GameMaker
        /// </summary>
        public static string? gameEngine;
        public static bool game_change;
        /// <summary>
        /// A (probably) temporary bool to tell if compareCombine() has been called
        /// </summary>
        public static bool combined;
        public static string? modTool;
        public static string[] xDeltaFile;
        public static string loadError;

        public static string GetLine(string fileName, int line) // what the fuck man
        {
            using (var sr = new StreamReader(fileName))
            {
                for (int i = 1; i < line; i++) sr.ReadLine();
                return sr.ReadLine();
            }
        }

        public static void CreateCombinerDirectories()
        {
            Directory.CreateDirectory($"{output}\\Cache\\vanilla");
            for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
                Directory.CreateDirectory($"{output}\\xDeltaCombiner\\{modNumber}\\Objects");
        }

        public static void PrepareVanillaFiles()
        {
            if (game_change)
            {
                string[] vanilla = Directory.GetFiles(@vanilla2, "*.win", SearchOption.AllDirectories);
                for (int modNumber = 0; modNumber < (modAmount + 1); modNumber++)
                {
                    Directory.CreateDirectory($"{output}\\xDeltaCombiner\\{modNumber}\\vanilla");
                    File.Copy(vanilla[modNumber], $"{output}\\xDeltaCombiner\\{modNumber}\\data.win", true);
                    File.Copy(vanilla[modNumber], $"{output}\\xDeltaCombiner\\{modNumber}\\vanilla\\data.win", true);
                }
                return;
            }

            for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
                File.Copy(@vanilla2, $"{output}\\xDeltaCombiner\\{modNumber}\\data.win", true);
        }
        
        public static void PerformMassPatch(string[] filepath = null)
        {
            int extra = game_change ? 1 : 2;
            int asdf = game_change ? 0 : 2; // WHAT THE FUCK IS AN ASDF BRO YOU AINT TOMSKA
            xDeltaFile = new string[modAmount + 2];

            for (int modNumber = asdf; modNumber < (modAmount + extra); modNumber++)
            {
                xDeltaFile[modNumber] = (filepath == null ? Console.ReadLine() : filepath[modNumber]).Replace("\"", "");
                string ext = Path.GetExtension(xDeltaFile[modNumber]);
                if (ext == ".csx")
                    using (var modToolProc = new Process())
                    {
                        modToolProc.StartInfo.FileName = @modTool;
                        modToolProc.StartInfo.Arguments = "load " + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win " + "--verbose --output " + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + " --scripts " + xDeltaFile[modNumber];
                        modToolProc.StartInfo.CreateNoWindow = false;
                        modToolProc.StartInfo.UseShellExecute = false;
                        modToolProc.StartInfo.RedirectStandardOutput = true;
                        modToolProc.Start();

                        // Synchronously read the standard output of the spawned process.
                        StreamReader reader = modToolProc.StandardOutput;
                        string ProcOutput = reader.ReadToEnd();

                        // Write the redirected output to this application's window.
                        Console.WriteLine(ProcOutput);

                        modToolProc.WaitForExit();
                    }
                else if (ext == ".win") File.Copy(xDeltaFile[modNumber], @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", true);
                else
                {
                    File.WriteAllText(@output + "\\Cache\\modNumbersCache.txt", Convert.ToString(modNumber));
                    using (var bashProc = new Process())
                    {
                        bashProc.StartInfo.FileName = @DeltaPatcher;
                        bashProc.StartInfo.Arguments = "-v -d -f -s \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win\" " + xDeltaFile[modNumber] + "\" \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\dat.win\"";
                        bashProc.StartInfo.CreateNoWindow = false;
                        bashProc.StartInfo.UseShellExecute = false;
                        bashProc.StartInfo.RedirectStandardOutput = true;
                        bashProc.Start();
                        // Synchronously read the standard output of the spawned process.
                        StreamReader reader = bashProc.StandardOutput;
                        string ProcOutput = reader.ReadToEnd();

                        // Write the redirected output to this application's window.
                        Console.WriteLine(ProcOutput);

                        bashProc.WaitForExit();
                    }
                    File.Delete(@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win");
                    File.Move(@output + "\\xDeltaCombiner\\" + modNumber + "\\dat.win", @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win");
                }
            }
        }

        public static List<string> modifedAssets = new List<string> { "Asset Name                       Hash (SHA1 in Base64)" };
        public static void CreateModifiedAssetsList()
        {
            if (!File.Exists(@output + "\\xDeltaCombiner\\1\\modifedAssets.txt"))
                File.Create(@output + "\\xDeltaCombiner\\1\\modifedAssets.txt").Close();
        }

        public static void DumpGameData()
        {
            for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
            {
                Directory.CreateDirectory(@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\CodeEntries");
                File.WriteAllText(@output + "\\Cache\\modNumbersCache.txt", modNumber.ToString());
                if (modNumber != 1)
                    using (var modToolProc = new Process())
                    {
                        modToolProc.StartInfo.FileName = @modTool;
                        modToolProc.StartInfo.Arguments = "load \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win\" --verbose --output \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ExportAllTexturesGrouped.csx\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ExportAllCode.csx\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ExportAssetOrder.csx\"";
                        modToolProc.StartInfo.CreateNoWindow = false;
                        modToolProc.StartInfo.UseShellExecute = false;
                        modToolProc.StartInfo.RedirectStandardOutput = true;
                        modToolProc.Start();

                        // Synchronously read the standard output of the spawned process.
                        StreamReader reader = modToolProc.StandardOutput;
                        string ProcOutput = reader.ReadToEnd();

                        // Write the redirected output to this application's window.
                        Console.WriteLine(ProcOutput);

                        modToolProc.WaitForExit();
                    }
            }
        }

        public static void CompareAndCombineAssetOrders()
        {
            string[] vanillaFiles = Directory.GetFiles(@output + "\\xDeltaCombiner\\0\\Objects", "*", SearchOption.AllDirectories);
            string[] vanillaFilesName = vanillaFiles.Select(Path.GetFileName).ToArray();
            int vanillaFileCount = vanillaFiles.Length;

            for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
            {
                string[] modFiles = Directory.GetFiles(@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects", "*", SearchOption.AllDirectories);
                int modFileCount = modFiles.Length;

                string[] modFilesName = modFiles.Select(Path.GetFileName).ToArray();
                string[] modFileAdditions = modFilesName.Except(vanillaFilesName).ToArray();
                int modFileAdditionsCount = modFileAdditions.Length;

                for (int i = 0; i < modFileCount; i++)
                {
                    string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFiles[i]))?.Name + "\\" + Directory.GetParent(modFiles[i])?.Name;
                    for (int j = 0; j < vanillaFileCount; j++)
                        if (vanillaFilesName[j] == modFilesName[i] && modFilesName[i] != "AssetOrder.txt")
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
                                                    try
                                                    {
                                                        using (Bitmap image1 = new(@modFiles[i]))
                                                        {

                                                            using (Bitmap image2 = new(@vanillaFiles[j]))
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
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine(ex.Message);
                                                        Console.WriteLine(modFiles[i]);
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

                if (modFileAdditionsCount >= 1)
                {
                    string[] modFileAddtionsDir = Directory.GetFiles(@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects", "*", SearchOption.AllDirectories).Select(Path.GetFullPath).ToArray();
                    for (int i = 0; i < modFileAdditionsCount; i++)
                    {
                        string[] modFilesParent = Directory.GetFiles(@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects", modFileAdditions[i], SearchOption.AllDirectories);
                        string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFilesParent[0]))?.Name + "\\" + Directory.GetParent(modFilesParent[0])?.Name;

                        if (modFileDir == (modNumber + "\\Objects"))
                        {
                            File.Copy(Path.GetDirectoryName(modFiles[0]) + "\\" + Path.GetFileName(modFilesParent[0]), @output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(modFileAdditions[i]), true);
                            modifedAssets.Add(Path.GetFileName(modFilesParent[0]) + "        ");
                        } else
                        {
                            Directory.CreateDirectory(@output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                            File.Copy(Directory.GetParent(modFilesParent[0]) + "\\" + Path.GetFileName(modFilesParent[0]), @output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(modFileAdditions[i]), true);
                            modifedAssets.Add(Path.GetFileName(modFilesParent[0]) + "        ");
                        }

                        Console.WriteLine("Currently Copying " + Path.GetFileName(modFileAdditions[i]));
                        Console.WriteLine(modFileAdditionsCount);
                    }
                }

                string assetOrderSeperator = "a bunch of random characters to define this";
                var vanillaAssetOrder = File.ReadAllLines(@output + "\\xDeltaCombiner\\0\\Objects\\AssetOrder.txt");
                int vanillaAssetOrderCount = vanillaAssetOrder.Length;
                var modAssetOrder = File.ReadAllLines(@output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\AssetOrder.txt");
                int modAssetOrderCount = modAssetOrder.Length;
                //if (!File.Exists(output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt"))
                //    File.Create(output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt").Close();

                var finalModAssetOrder = File.ReadAllLines(@output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt").ToList();
                for (int i = 0; i < vanillaAssetOrderCount; i++)
                {
                    Console.WriteLine("Comparing asset order of vanilla to mod # " + (modNumber - 1) + ", Line " + i);

                    string vanillaAssetOrderLine = vanillaAssetOrder[i];
                    for (int j = 0; j < modAssetOrderCount; j++)
                    {
                        string modAssetOrderLine = modAssetOrder[j];
                        bool modAssetOrderinvanillaAssetOrder = vanillaAssetOrder.Contains(modAssetOrderLine);
                        if (modAssetOrderLine == vanillaAssetOrderLine && modAssetOrderLine.StartsWith("@@"))
                            assetOrderSeperator = modAssetOrderLine;

                        if (modAssetOrderLine == vanillaAssetOrderLine && !finalModAssetOrder.Contains(assetOrderSeperator))
                            if (modNumber == 2)
                                finalModAssetOrder.Add(modAssetOrderLine);
                            else
                                finalModAssetOrder.Insert(j, vanillaAssetOrderLine);

                        if (modAssetOrderLine != vanillaAssetOrderLine && !modAssetOrderinvanillaAssetOrder && !finalModAssetOrder.Contains(modAssetOrderLine))
                            if (modNumber == 2)
                                finalModAssetOrder.Add(modAssetOrderLine);
                            else
                                finalModAssetOrder.Insert(j, modAssetOrderLine);
                    }

                    // this is completely useless? it gets overriden?? what is this for???
                    //File.WriteAllLines(@output + "\\xDeltaCombiner\\1\\Objects\\AssetOrder.txt", modAssetOrder);
                }
            }
            combined = true;
        }

        public static void ImportFromCombine()
        {
            if (modTool == "skip")
            {
                Console.WriteLine("In order to replace and import manually, load up the data.win in \\xDeltaCombiner\\1\\ into the GUI version of UTMT and run the script ImportGML.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\" as the import folder. Once finished, exit and saving.");
                return;
            }

            using (var modToolProc = new Process())
            {
                modToolProc.StartInfo.FileName = @modTool;
                modToolProc.StartInfo.Arguments = "load \"" + @output + "\\xDeltaCombiner\\1\\data.win\" --verbose --output \"" + @output + "\\xDeltaCombiner\\1\\data.win\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ImportGraphicsAdvanced.csx\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ImportGML.csx\" --scripts \"" + @pwd + "\\UTMTCLI\\Scripts\\ImportAssetOrder.csx\"";
                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.StartInfo.UseShellExecute = false;
                modToolProc.StartInfo.RedirectStandardOutput = true;
                modToolProc.Start();

                // Synchronously read the standard output of the spawned process.
                StreamReader reader = modToolProc.StandardOutput;
                string ProcOutput = reader.ReadToEnd();

                // Write the redirected output to this application's window.
                Console.WriteLine(ProcOutput);
                modToolProc.WaitForExit();
            }
        }
        public static void SaveResult(string modname)
        {
            if (modname != null && modname != "")
            {
                if (combined)
                {
                    Directory.CreateDirectory(@output + "\\result\\" + modname + "\\");
                    using (var bashProc = new Process())
                    {
                        bashProc.StartInfo.FileName = @DeltaPatcher;
                        bashProc.StartInfo.Arguments = "-v -e -f -s \"" + @output + "\\xDeltaCombiner\\0\\data.win\" \"" + @output + "\\xDeltaCombiner\\1\\data.win\" \"" + @output + "\\result\\" + modname + "\\" + modname + ".xdelta\"";
                        bashProc.StartInfo.CreateNoWindow = false;
                        bashProc.Start();
                        bashProc.WaitForExit();
                    }

                    File.Copy(@output + "\\xDeltaCombiner\\1\\data.win", @output + "\\result\\" + modname + "\\data.win");
                    File.Copy(@output + "\\xDeltaCombiner\\1\\modifedAssets.txt", @output + "\\result\\" + modname + "\\modifedAssets.txt");
                }
                else
                {
                    for (int modNumber = 0; modNumber < (modAmount + 1); modNumber++)
                    {
                        Directory.CreateDirectory(@output + "\\result\\" + modname + "\\" + modNumber);
                        using (var bashProc = new Process())
                        {
                            bashProc.StartInfo.FileName = @DeltaPatcher;
                            bashProc.StartInfo.Arguments = "-v -e -f -s \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\vanilla\\data.win\" \"" + @output + "\\xDeltaCombiner\\" + modNumber + "\\data.win\" \"" + @output + "\\result\\" + modname + "\\" + modNumber + ".xdelta\"";
                            bashProc.StartInfo.CreateNoWindow = false;
                            bashProc.Start();
                            bashProc.WaitForExit();
                        }
                        File.Copy(@output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", @output + "\\result\\" + modname + "\\" + modNumber + "\\data.win");
                    }
                }
            }
        }
        public static void ClearCache()
        {
            Directory.Delete(@output + "\\xDeltaCombiner\\", true);
        }

        /// <summary>
        /// Loads Template
        /// </summary>
        /// <param name="filepath"></param>
        public static void LoadModTemplate(string filepath = null)
        {
            if (filepath == null)
                if (File.Exists($"{pwd}\\template.xrune"))
                    filepath = $"{pwd}\\template.xrune";

            if (filepath != null)
            {
                if (GetLine(filepath, 1) == "0.4")
                {
                    string OpToPerform = GetLine(filepath, 2);
                    modAmount = int.Parse(GetLine(filepath, 3));
                    vanilla2 = GetLine(filepath, 4);
                    output = GetLine(filepath, 5);
                    DeltaPatcher = GetLine(filepath, 6);
                    modTool = GetLine(filepath, 7);
                    game_change = GetLine(filepath, 10).ToLower() == "true"; // True...
                    if (OpToPerform == "regular")
                    {
                        CreateCombinerDirectories();
                        PrepareVanillaFiles();
                        PerformMassPatch(GetLine(filepath, 8).Split(",").ToArray());
                        CreateModifiedAssetsList();
                        CompareAndCombineAssetOrders();
                        SaveResult(GetLine(filepath, 9));
                    }
                }
                else
                    loadError = "The template's version is not supported";
            }

            if (filepath == null)
                loadError = "The Template doesn't exists";
        }
    }
}
//A logging class C+Ped from https://stackoverflow.com/questions/420429/mirroring-console-output-to-a-file
class ConsoleCopy : IDisposable
{

    FileStream fileStream;
    StreamWriter fileWriter;
    TextWriter doubleWriter;
    TextWriter oldOut;

    class DoubleWriter : TextWriter
    {

        TextWriter one;
        TextWriter two;

        public DoubleWriter(TextWriter one, TextWriter two)
        {
            this.one = one;
            this.two = two;
        }

        public override Encoding Encoding
        {
            get { return one.Encoding; }
        }

        public override void Flush()
        {
            one.Flush();
            two.Flush();
        }

        public override void Write(char value)
        {
            one.Write(value);
            two.Write(value);
        }

    }

    public ConsoleCopy(string path)
    {
        oldOut = Console.Out;

        try
        {
            fileStream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);

            fileWriter = new StreamWriter(fileStream);
            fileWriter.AutoFlush = true;

            doubleWriter = new DoubleWriter(fileWriter, oldOut);
        }
        catch (Exception e)
        {
            Console.WriteLine("Cannot open file for writing");
            Console.WriteLine(e.Message);
            return;
        }
        Console.SetOut(doubleWriter);
    }

    public void Dispose()
    {
        Console.SetOut(oldOut);
        if (fileWriter != null)
        {
            fileWriter.Flush();
            fileWriter.Close();
            fileWriter = null;
        }

        if (fileStream != null)
        {
            fileStream.Close();
            fileStream = null;
        }
    }

}

//A console bool response thingy from https://stackoverflow.com/a/54127216
class UtilsConsole
{
    public static bool Confirm(string title)
    {
        ConsoleKey response;
        do
        {
            Console.Write($"{title} [y/n] ");
            response = Console.ReadKey(false).Key;

            if (response != ConsoleKey.Enter)
                Console.WriteLine();
        } while (response != ConsoleKey.Y && response != ConsoleKey.N);

        return (response == ConsoleKey.Y);
    }
}

