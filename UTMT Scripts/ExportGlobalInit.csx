using System.Text;
using System;
using System.IO;
using System.Reflection;
using UndertaleModLib;
using UndertaleModLib.Models;

EnsureDataLoaded();

//string chapterNo = File.ReadAllText(@Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location)))) + "/output/Cache/running/chapterNumber.txt"));
//string modNo = File.ReadAllText(@Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location)))) + "/output/Cache/running/modNumbersCache.txt"));
//string outputPath = @Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location))))) + "/output/xDeltaCombiner/"+chapterNo+"/"+modNo+"/Objects/AssetOrder.txt";
string outputPath = "./test.txt";
if (string.IsNullOrWhiteSpace(@outputPath))
{
    return;
}
//Approach 1: modify ExportAssetOrder.csx to export that line in UndertaleData.cs
 /**   var qwerty = new UndertaleModLib.UndertaleChunkFORM();
    IList<UndertaleModLib.Models.UndertaleGlobalInit> assets => qwerty.GLOB?.List;
void WriteAssetNames(StreamWriter writer)

{
    if (assets.Count == 0)
        return;
    foreach (var asset in assets)
    {
        if (asset is not null)
            writer.WriteLine(asset.ToString());
            //writer.WriteLine(asset.Name?.Content ?? assets.IndexOf(asset).ToString());
        else
            writer.WriteLine("(null)");
    }
}
string test {get; set;}
using (StreamWriter writer = new StreamWriter(outputPath))
{
    WriteAssetNames(writer);
    
}*/
//Approach 2: Attempt to use the Serialize method in the UndertaleGlobalInit class
/**var q = new UndertaleModLib.Models.UndertaleCode { get => _code.Resource; set { _code.Resource = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code))); } }
var qwerty=new UndertaleModLib.Models.UndertaleGlobalInit();
qwerty.Serialize(new UndertaleWriter(new FileStream(outputPath, FileMode.CreateNew)))*/
//Approach 3: modify the globalinit excerpt from ConvertFrom17to16_for_2.3.csx
void WriteInitNames(StreamWriter writer){
    for (int i = 0; i < Data.Scripts.Count; i++)
    {
        UndertaleScript script = Data.Scripts[i];
        if (script.Name.Content.Contains("gml_Script_"))
        {
            UndertaleScript scr_dup = Data.Scripts.ByName(script.Name.Content.Replace("gml_Script_", ""));
            if (scr_dup != null)
            {
                UndertaleCode scr_dup_code = scr_dup.Code;
                if (scr_dup_code != null)
                {
                    UndertaleString scr_dup_code_name = scr_dup_code.Name;
                    if (scr_dup_code_name != null)
                    {
                        string scr_dup_code_name_con = scr_dup_code_name.Content;                    
                    foreach (UndertaleGlobalInit globalInit in Data.GlobalInitScripts)
                        {
                            if (globalInit.Code.Name.Content == scr_dup_code_name_con)
                            {
                                                                  
                                    writer.WriteLine(globalInit.Code.Name.Content.ToString());
                                
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
using (StreamWriter writer = new StreamWriter(outputPath))
{
    WriteInitNames(writer);
    
}