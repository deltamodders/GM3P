using System.Text;
using System;
using System.IO;
using System.Reflection;
using UndertaleModLib;
using UndertaleModLib.Models;

EnsureDataLoaded();

string chapterNo = File.ReadAllText(@Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location)))))))) + "/output/Cache/running/chapterNumber.txt"));
string modNo = File.ReadAllText(@Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location)))))))) + "/output/Cache/running/modNumbersCache.txt"));
string outputPath = @Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Directory.GetParent(Convert.ToString(Assembly.GetEntryAssembly().Location))))))))) + "/output/xDeltaCombiner/"+chapterNo+"/"+modNo+"/Objects/GlobalInit.txt";
//string outputPath = "./test.txt";
if (string.IsNullOrWhiteSpace(@outputPath))
{
    return;
}
string[] lines = File.ReadAllLines(@outputPath);
List<string> currentList = new List<string>();
foreach (string line in lines)
{
    if (!string.IsNullOrWhiteSpace(line))
    {
        currentList.Add(line.Trim());
    }
        
}
//var target = IList<UndertaleGlobalInit>;
SubmitList();
void SubmitList(){
    Data.GlobalInitScripts.Clear();
    foreach (var item in currentList){
        //string? codeName = item.GetString();
        if (string.IsNullOrWhiteSpace(item)) continue;

        var code = Data.Code?.ByName(item);
        if (code == null)
        {
            ScriptMessage($"[ImportGlobalScripts] Missing code ref '{item}', skipped.");
            continue;
        }
    Data.GlobalInitScripts.Add(new UndertaleGlobalInit{Code = code});
    }
}