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
using GM3P.modNumbers;

double Version = 0.3;
Console.WriteLine("GM3P v" + Version + ".0-alpha2");
Console.WriteLine("Insert the path to the vanilla data.win, or type \"skip\" if you just want to compare and combine:");
string? vanilla2 = Console.ReadLine();
string vanilla = vanilla2.Replace("\"","");
string pwd = Convert.ToString(Directory.GetParent(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
string output = pwd + "\\output";


dynamic modNo = new ModNumbers();





Console.WriteLine("Type however many mods you want to patch: ");
int modAmount = Convert.ToInt32(Console.ReadLine());
if (vanilla != "skip")
{
    //Console.WriteLine("Enter in the path of a xdelta3 executable: ");
    string DeltaPatcher2 = pwd + "\\xdelta3-3.0.11-x86_64.exe";
    string DeltaPatcher = DeltaPatcher2.Replace("\"", "");
    Directory.CreateDirectory(output + @"\xDeltaCombiner");
    for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
    {

        Directory.CreateDirectory(output + "\\xDeltaCombiner\\" + modNumber);
        Directory.CreateDirectory(output + "\\xDeltaCombiner\\" + modNumber + "\\Objects");
        File.Copy(@vanilla, output + "\\xDeltaCombiner\\" + modNumber + "\\data.win", true);

    }
    ;
    string[] xDeltaFile2 = new string[(modAmount + 2)];
    string[] xDeltaFile = new String[(modAmount + 2)];
    string[] xDeltaFileLinux2 = new string[(modAmount + 2)];
    string[] xDeltaFileLinux = new string[(modAmount + 2)];
    Console.WriteLine("Now Enter in the xDeltas, one at a time: ");
    for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
    {
        xDeltaFile2[modNumber] = Console.ReadLine();
        
    }
    ;

    for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
    {
        File.WriteAllText(output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
        modNo.ModNumber = modNumber;
        xDeltaFile[modNumber] = xDeltaFile2[modNumber].Replace("\"", "");
        xDeltaFileLinux2[modNumber] = xDeltaFile[modNumber].Replace("\\", "/");
        xDeltaFileLinux[modNumber] = xDeltaFileLinux2[modNumber].Replace("C:", "c");
        using (var bashProc = new Process())
        {
            bashProc.StartInfo.FileName = DeltaPatcher;
            bashProc.StartInfo.Arguments = "-v -d -f -s " + output + "\\xDeltaCombiner\\0\\data.win" + " \"" + xDeltaFile[modNumber] + "\" \""+output+"\\xDeltaCombiner\\" + modNumber + "\\data.win" + "\" ";
            bashProc.StartInfo.CreateNoWindow = false;
            bashProc.Start();
            bashProc.WaitForExit();
        }
    }
}
Console.WriteLine("Enter in the Mod Tool (e.g. UnderTaleModTool for GameMaker Games). If you want to use the included tool, just hit enter. If you want to manually dump and import enter \"skip\"");
Console.WriteLine("If you don't want to combine patches and just wanted to apply them, you may exit the terminal now");
string? modTool = Console.ReadLine();
if (modTool == null || modTool == "")
{
    modTool = pwd + "\\UTMTCLI\\UndertaleModCli.exe";
}
if (modTool != "skip")
{
    for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
    {
        File.WriteAllText(output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
        if (modNumber != 1)
        {
            using (var modToolProc = new Process())
            {
                modToolProc.StartInfo.FileName = @modTool;
                modToolProc.StartInfo.Arguments = "dump " + output + "\\xDeltaCombiner\\" + modNumber + "\\data.win " + "--verbose --output " + output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\" + " --code UMT_DUMP_ALL ";
                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.Start();
                modToolProc.WaitForExit();
            }
        }
    }
    Console.WriteLine("The code dumping process(es) are finish, then hit enter");
    Console.ReadLine();
    for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
    {
        File.WriteAllText(output + "\\modNumbersCache.txt", Convert.ToString(modNumber));
        if (modNumber != 1)
        {
            modNo.ModNumber = modNumber;
            using (var modToolProc = new Process())
            {
                modToolProc.StartInfo.FileName = @modTool;
                modToolProc.StartInfo.Arguments = "load " + output + "\\xDeltaCombiner\\" + modNumber + "\\data.win " + "--verbose --output " + output + "\\xDeltaCombiner\\" + modNumber + "\\data.win" + " --scripts " + pwd + "\\UTMTCLI\\Scripts\\ExportAllTexturesGrouped.csx";
                modToolProc.StartInfo.CreateNoWindow = false;
                modToolProc.Start();
                modToolProc.WaitForExit();
            }
        }
    }
    Console.WriteLine("The sprite dumping process(es) are finished");
}
if (modTool == "skip")
{
    Console.WriteLine("In order to dump manually, load up the data.win in each of the \\xDeltaCombiner\\ subfolders into the GUI version of UTMT and run the script ExportAllCode.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\\\" as your destination. Once finished, exit without saving.");
    Console.WriteLine("Press Enter when done with the above instructions");
}
Console.ReadLine();
for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
{
    int vanillaFileCount = Convert.ToInt32(Directory.GetFiles("" + output + "\\xDeltaCombiner\\0\\Objects\\", "*", SearchOption.AllDirectories).Length);
    int modFileCount = Convert.ToInt32(Directory.GetFiles("" + output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories).Length);
    string[] vanillaFiles = Directory.GetFiles("" + output + "\\xDeltaCombiner\\0\\Objects\\", "*", SearchOption.AllDirectories);
    string[] modFiles = Directory.GetFiles("" +output + "\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories);
    for (int i = 0; i < modFileCount; i++)
    {
        int k = 0;
        string? modFileDir = Directory.GetParent(Path.GetDirectoryName(modFiles[i])).Name + "\\" + Directory.GetParent(modFiles[i]).Name;
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

            //                File.Copy(Path.GetDirectoryName(vanillaFiles[k2]) + "\\" + Path.GetFileName(vanillaFiles[k2]), output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(vanillaFiles[k2]), true);
            //            }
            //            if (vanillaFileDir != ("0\\Objects"))
            //            {
            //                Directory.CreateDirectory(output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

            //                File.Copy(Path.GetDirectoryName(vanillaFiles[k2]) + "\\" + Path.GetFileName(vanillaFiles[k2]), output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[k2]), true);
            //            }
            //        }
            //        Console.WriteLine("Copying" + vanillaFiles[k2]);
            //    }
            //    k++;
            //}
            if (Path.GetFileName(vanillaFiles[j]) == Path.GetFileName(modFiles[i]))
            {
                Console.WriteLine("Currently Comparing " + Path.GetFileName(vanillaFiles[j])+ " to " + Path.GetFileName(modFiles[i]));

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
                                        File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\CodeEntries" + Path.GetFileName(vanillaFiles[j]), true);
                                    }
                                    if (modFileDir != ("Objects\\CodeEntries"))
                                    {
                                        

                                        if (Path.GetExtension(modFiles[i]) == ".png")
                                        {


                                            Bitmap image1 = new Bitmap(modFiles[i]);
                                            Bitmap image2 = new Bitmap(vanillaFiles[j]);

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
                                            Directory.CreateDirectory(output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                                            File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[j]), true);
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

                                        File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(vanillaFiles[j]), true);
                                    }
                                    if (modFileDir != (modNumber + "\\Objects"))
                                    {
                                        Directory.CreateDirectory(output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                                        File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(vanillaFiles[j]), true);
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
        if(i>vanillaFileCount)
        {
            if (modFileDir == (modNumber + "\\Objects"))
            {

                File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(modFiles[i]), true);
            }
            if (modFileDir != (modNumber + "\\Objects"))
            {
                Directory.CreateDirectory(output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir);

                File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), output + "\\xDeltaCombiner\\1\\Objects\\" + modFileDir + "\\" + Path.GetFileName(modFiles[i]), true);
            }
            Console.WriteLine("Currently Copying" + Path.GetFileName(modFiles[i]));
        }
    }

}
Console.WriteLine("Comparing is done. Hit Enter to Continue.");
Console.ReadLine();
if (modTool == "skip")
{
    Console.WriteLine("In order to replace and import manually, load up the data.win in \\xDeltaCombiner\\1\\ into the GUI version of UTMT and run the script ImportGML.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\\\" as the import folder. Once finished, exit and saving.");
}
if (modTool != "skip")
{
    using (var modToolProc = new Process())
    {
        modToolProc.StartInfo.FileName = @modTool;
        modToolProc.StartInfo.Arguments = "replace " + output + "\\xDeltaCombiner\\1\\data.win " + "--verbose --output " + output + "\\xDeltaCombiner\\1\\data.win" + " --code UMT_REPLACE_ALL="+ output+ "\\xDeltaCombiner\\1\\Objects\\";
        modToolProc.StartInfo.CreateNoWindow = false;
        modToolProc.Start();
        modToolProc.WaitForExit();
    }
    //using (var modToolProc = new Process())
    //{
    //    modToolProc.StartInfo.FileName = @modTool;
    //    modToolProc.StartInfo.Arguments = "load " + output + "\\xDeltaCombiner\\1\\data.win " + "--verbose --output " + output + "\\xDeltaCombiner\\1\\data.win" + " --scripts " + pwd + "\\UTMTCLI\\Scripts\\DisposeAllEmbeddedTextures.csx";
    //    modToolProc.StartInfo.CreateNoWindow = false;
    //    modToolProc.Start();
    //    modToolProc.WaitForExit();
    //}
    using (var modToolProc = new Process())
    {
        modToolProc.StartInfo.FileName = @modTool;
        modToolProc.StartInfo.Arguments = "load " + output + "\\xDeltaCombiner\\1\\data.win " + "--verbose --output " + output + "\\xDeltaCombiner\\1\\data.win" + " --scripts " + pwd + "\\UTMTCLI\\Scripts\\ImportGraphicsAdvanced.csx";
        modToolProc.StartInfo.CreateNoWindow = false;
        modToolProc.Start();
        modToolProc.WaitForExit();
    }
}
Console.WriteLine("Press Enter To Clean up (Will delete output\\xDeltaCombiner) and exit");
Console.ReadLine();
for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
{
    if (modNumber != 1)
    {
        Directory.Delete(output + "\\xDeltaCombiner\\" + modNumber, true);
    }
}