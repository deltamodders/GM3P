// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System;
using System.Security.Cryptography;
using System.Text;

Console.WriteLine("Mass Mod Patcher (GameMaker games only ATM)");
Console.WriteLine("Insert the path to the vanilla data.win, or type \"skip\" if you just want to compare and combine:");
string? vanilla2 = Console.ReadLine();
string vanilla = vanilla2.Replace("\"","");

Console.WriteLine("Type however many mods you want to patch: ");
int modAmount = Convert.ToInt32(Console.ReadLine());
if (vanilla != "skip")
{
    string DeltaPatcher = @"F:\Downloads\xdelta3-3.1.0-x86_64.exe";
    Directory.CreateDirectory(@"C:\xDeltaCombiner");
    for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
    {

        Directory.CreateDirectory("C:\\xDeltaCombiner\\" + modNumber);
        Directory.CreateDirectory("C:\\xDeltaCombiner\\" + modNumber + "\\Objects");
        File.Copy(@vanilla, "C:\\xDeltaCombiner\\" + modNumber + "\\data.win", true);

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
        xDeltaFile[modNumber] = xDeltaFile2[modNumber].Replace("\"", "");
        xDeltaFileLinux2[modNumber] = xDeltaFile[modNumber].Replace("\\", "/");
        xDeltaFileLinux[modNumber] = xDeltaFileLinux2[modNumber].Replace("C:", "c");
        using (var bashProc = new Process())
        {
            bashProc.StartInfo.FileName = DeltaPatcher;
            bashProc.StartInfo.Arguments = "-d -f -s " + "C:\\xDeltaCombiner\\0\\data.win" + " \"" + xDeltaFile[modNumber] + "\" c:\\xDeltaCombiner\\" + modNumber + "\\data.win" + " ";
            bashProc.StartInfo.CreateNoWindow = false;
            bashProc.Start();
        }
    }
}
Console.WriteLine("Wait for DeltaPatcher to finish applying patches, then enter in the Mod Tool (e.g. UnderTaleModTool for GameMaker Games)");
Console.WriteLine("If you don't want to combine patches and just wanted to apply them, you may exit the terminal once DeltaPatcher is done");
string? modTool = Console.ReadLine();
/**
for (int modNumber = 0; modNumber < (modAmount + 2); modNumber++)
{
    using (var modToolProc = new Process())
    {
        modToolProc.StartInfo.FileName = @modTool;
        modToolProc.StartInfo.Arguments = "--verbose --output "+ "C:\\xDeltaCombiner\\" + modNumber + "\\Objects\\" + " --code UMT_DUMP_ALL dump "+ "C:\\xDeltaCombiner\\" + modNumber + "\\data.win";
        modToolProc.StartInfo.CreateNoWindow = false;
        modToolProc.Start();
    }
}
Console.WriteLine("Wait for the dumping process(es) to finish, then hit enter");
*/
Console.WriteLine("Unfortantly, due to a bug from the CLI version of UTMT, this program cannot auto-dump the contents for you.");
Console.WriteLine("In order to dump manually, load up the data.win in each of the C:\\xDeltaCombiner\\ subfolders into the GUI version of UTMT and run the script ExportAllCode.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\\\" as your destination. Once finished, exit without saving.");
Console.WriteLine("Press Enter when done with the above instructions");
Console.ReadLine();
for (int modNumber = 2; modNumber < (modAmount + 2); modNumber++)
{
    int vanillaFileCount = Convert.ToInt32(Directory.GetFiles(@"C:\xDeltaCombiner\0\Objects\", "*", SearchOption.AllDirectories).Length);
    int modFileCount = Convert.ToInt32(Directory.GetFiles("C:\\xDeltaCombiner\\"+ modNumber + "\\Objects\\", "*", SearchOption.AllDirectories).Length);
    string[] vanillaFiles = Directory.GetFiles(@"C:\xDeltaCombiner\0\Objects\", "*", SearchOption.AllDirectories);
    string[] modFiles = Directory.GetFiles("C:\\xDeltaCombiner\\" + modNumber + "\\Objects\\", "*", SearchOption.AllDirectories);
    for (int i = 0; i < modFileCount; i++)
    {
        
        for (int j = 0; j < vanillaFileCount; j++)
        {
            if (Path.GetFileName(vanillaFiles[j]) == Path.GetFileName(modFiles[i]))
            {
                Console.WriteLine("Currently Comparing " + Path.GetFileName(vanillaFiles[j])+ " to " + Path.GetFileName(modFiles[i]));
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
                            File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), "C:\\xDeltaCombiner\\1\\Objects\\" + Path.GetFileName(vanillaFiles[j]), true);
                        }

                    }
                }

            }

        }
        if(i>vanillaFileCount)
        {

            File.Copy(Path.GetDirectoryName(modFiles[i]) + "\\" + Path.GetFileName(modFiles[i]), @"C:\xDeltaCombiner\1\Objects\" + "\\" + Path.GetFileName(modFiles[i]), true);
        }
    }

}
Console.WriteLine("Comparing is done");
Console.WriteLine("Unfortantly, due to a bug from the CLI version of UTMT, this program cannot auto-import the contents for you.");
Console.WriteLine("In order to replace and import manually, load up the data.win in C:\\xDeltaCombiner\\1\\ into the GUI version of UTMT and run the script ImportGML.csx. Select \"C:\\xDeltaCombiner\\*currentsubfolder*\\Objects\\\" as the import folder. Once finished, exit and saving.");
/**using (var modToolProc = new Process())
{
    modToolProc.StartInfo.FileName = @modTool;
    modToolProc.StartInfo.Arguments = "-v -o " + "C:\\xDeltaCombiner\\1\\Objects\\" + " - c UMT_REPLACE_ALL replace C:\\xDeltaCombiner\\1\\Objects\\" + "C:\\xDeltaCombiner\\1\\data.win";
    modToolProc.StartInfo.CreateNoWindow = false;
    modToolProc.Start();
}*/