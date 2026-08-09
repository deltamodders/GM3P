using GM3P.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace GM3P.Data
{
    public class DeltaModPackage
    {
        public DeltaModPackageJSON? ModPackageJSON { get; set; }
        public DeltaModPackageXML? ModPackageXML { get; set; }
    }
    public interface IDeltaModPackageService
    {
        DeltaModPackageXML xmlDoc { get; }
        void UpdateJSON(Action<DeltaModPackageJSON> updateAction);
        void LoadJSON(string? configPath = null);
        void SaveJSON(string? configPath = null);
        Task UpdateXML(string updateAction, string[] entry, int entryNo);
        void ParseXML(string xmlContent);
        Task ExportXML(string xmlPath, string? result, GM3PConfig config);
    }
    public class DeltaModPackageJSON
    {
        public Dictionary<string, DeltaModPackagemetadata>? metadata { get; set; }
        public Dictionary<string, DeltaModPackageneededFiles>? neededFiles { get; set; }
        public string deltaruneTargetVersion { get; set; } = "1.0.0";
        public int UTMTversion { get; set; } = 8;
        public Dictionary<string, DeltaModPackageExporter>? exporter { get; set; }
    }
    public class DeltaModPackagemetadata {
        public string name { get; set; }
        public string? version { get; set; }
        public string[]? author { get; set; }
        public string? description { get; set; }
        public string? url { get; set; }
        public Dictionary<string, DeltaModPackageColor>? color { get; set; }
        public string game { get; set; } = "com.toby.deltarune";
        public string? packageID { get; set; }

    }
    public class DeltaModPackageColor { 
        public string? r { get; set; }
        public string? g { get; set; }
        public string? b { get; set; }
    }
    public class DeltaModPackageneededFiles { 
        public string? file { get; set; }
        public string? checksum { get; set; }
    }
    public class DeltaModPackageExporter {
        public string? tool { get; set; } = "GM3P";
    }
    public class DeltaModPackageXML
    {
        public List<string> type { get; set; } = [];
        public List<string> patch { get; set; } = [];
        public List<string> to { get; set; } = [];

    }
    public class DeltaModPackageService : IDeltaModPackageService
    {
        private IConfigurationService _config;
        private readonly string _defaultConfigPath;
        private IDeltaModPackageService _deltaModPackageService;
        private DeltaModPackageJSON _json;
        private DeltaModPackageXML _xmlDoc;

        //public static DeltaModPackageXML _xmlDoc = new DeltaModPackageXML();

        public DeltaModPackageXML xmlDoc => _xmlDoc;
        public DeltaModPackageJSON ModPackageJSON => _json;
        public DeltaModPackageJSON DeltaModPackageJSON { get; set; } = new DeltaModPackageJSON();
        public DeltaModPackageService()
        {
            var pwd = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            _defaultConfigPath = Path.Combine(pwd, "meta.json");
            _xmlDoc = LoadFromEnvironment();
        }
        public async Task UpdateXML(string updateAction, string[] entry, int entryNo)
        {

            if (updateAction == "change")
            {
                _xmlDoc.type[entryNo] = entry[0];
                _xmlDoc.patch[entryNo] = entry[1];
                _xmlDoc.to[entryNo] = entry[2];
            }
            else if (updateAction == "add")
            {
                _xmlDoc.type.Add(entry[0]);
                _xmlDoc.patch.Add(entry[1]);
                _xmlDoc.to.Add(entry[2]);
            }
            else if (updateAction == "remove")
            {
                if (entryNo >= 0 && entryNo < _xmlDoc.type.Count())
                {
                    _xmlDoc.type = _xmlDoc.type.Where((_, index) => index != entryNo).ToList();
                    _xmlDoc.patch = _xmlDoc.patch.Where((_, index) => index != entryNo).ToList();
                    _xmlDoc.to = _xmlDoc.to.Where((_, index) => index != entryNo).ToList();
                }
            }
            DeltaModPackageXML updatedXmlDoc = new DeltaModPackageXML
            {
                type = _xmlDoc.type,
                patch = _xmlDoc.patch,
                to = _xmlDoc.to
            };
        }
        public List<string> xmlContent = [];
        public async Task ExportXML(string xmlPath, string? result = null, GM3PConfig config = null)
        {
            
            if (!String.IsNullOrWhiteSpace(result))
            {
                for (int i = 0; i < config.ChapterAmount+1; i++)
                {
                    try { 
                    string[] xdeltas = Directory.GetFiles(Path.Combine(config.OutputPath, "result", result, i.ToString()), "*.xdelta");
                    string[] g3mpatches = Directory.GetFiles(Path.Combine(config.OutputPath, "result", result, i.ToString()), "*.g3mpatch");
                    string[] utmtscripts = Directory.GetFiles(Path.Combine(config.OutputPath, "result", result, i.ToString()), "*.csx");
                    if (xdeltas.Length > 0)
                    {
                        List<string> xdeltaPaths = [];
                        foreach (string xdel in xdeltas) { 
                            xdeltaPaths.Add(Path.GetRelativePath(Path.Combine(config.OutputPath, "result", result),xdel).Replace($"{i}\\",$"./{i}/"));
                        }
                            Console.WriteLine(xdeltaPaths[0]);
                        foreach (var xd in xdeltaPaths)
                        {
                            if (i == 0) {
                                xmlContent.Add($"<patch type=\"xdelta\" patch=\"{xd}\" to=\"./data.win\" />");
                            }
                            else
                            {
                                xmlContent.Add($"<patch type=\"xdelta\" patch=\"{xd}\" to=\"./chapter{i}_windows/data.win\" />");
                            }
                        }
                    }
                    if (g3mpatches.Length > 0)
                    {
                        List<string> g3mpatchPaths = [];
                        foreach (string xdel in g3mpatches)
                        {
                            g3mpatchPaths.Add(Path.GetRelativePath(Path.Combine(config.OutputPath, "result", result), xdel));
                        }
                        foreach (var xd in g3mpatchPaths)
                        {
                            if (i == 0)
                            {
                                xmlContent.Add($"<patch type=\"g3mpatch\" patch=\"{xd}\" to=\"./data.win\" />");
                            }
                            else
                            {
                                xmlContent.Add($"<patch type=\"g3mpatch\" patch=\"{xd}\" to=\"./chapter{i}_windows/data.win\" />");
                            }
                        }
                    }
                    if (utmtscripts.Length > 0)
                    {
                        List<string> utmtscriptPaths = [];
                        foreach (string xdel in utmtscripts)
                        {
                            utmtscriptPaths.Add(Path.GetRelativePath(Path.Combine(config.OutputPath, "result", result), xdel));
                        }
                        foreach (var xd in utmtscriptPaths)
                        {
                            if (i == 0)
                            {
                                xmlContent.Add($"<patch type=\"csx\" patch=\"{xd}\" to=\"./data.win\" />");
                            }
                            else
                            {
                                xmlContent.Add($"<patch type=\"csx\" patch=\"{xd}\" to=\"./chapter{i}_windows/data.win\" />");
                            }
                        }
                    }
                    }
                    catch { }
                }
            }
            else
            {
                for (int i = 0; i < _xmlDoc.type.Count(); i++)
                {
                    xmlContent.Append($"<patch type=\"{_xmlDoc.type[i]}\" patch=\"{_xmlDoc.patch[i]}\" to=\"{_xmlDoc.to[i]}\" />");
                }
            }
            File.WriteAllLines(xmlPath, xmlContent);
            var matches = Regex.Matches(String.Join(",",xmlContent), @"<patch\s+type=""([^""]+)""\s+patch=""([^""]+)""\s+to=""([^""]+)""\s*/>");
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count == 4)
                {
                    string type = match.Groups[1].Value;
                    _xmlDoc.type.Add(type);
                    string patch = match.Groups[2].Value;
                    _xmlDoc.patch.Add(patch);
                    string to = match.Groups[3].Value;
                    _xmlDoc.to.Add(to);
                    Console.WriteLine($"Type: {type}, Patch: {patch}, To: {to}");
                }
            }
        }
        public void ParseXML(string xml)
        {
            string xmlContent = "";
            if (xml != "mem")
            {
                xmlContent = File.ReadAllText(xml);
            }
            else
            {
                for (int i = 0; i < _xmlDoc.type.Count(); i++)
                {
                    Console.WriteLine($"Type: {_xmlDoc.type[i]}, Patch: {_xmlDoc.patch[i]}, To: {_xmlDoc.to[i]}");
                }
                
                /**Console.WriteLine("Enter XML content (end with an empty line):");
                StringBuilder xmlContentBuilder = new StringBuilder();
                string? line;
                while ((line = Console.ReadLine()) != null && line != "")
                {
                    xmlContentBuilder.AppendLine(line);
                }
                string xmlContent = xmlContentBuilder.ToString();*/
            }
            if (xml != "mem")
            {
                var matches = Regex.Matches(xmlContent, @"<patch\s+type=""([^""]+)""\s+patch=""([^""]+)""\s+to=""([^""]+)""\s*/>");
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count == 4)
                    {
                        string type = match.Groups[1].Value;
                        _xmlDoc.type.Add(type);
                        string patch = match.Groups[2].Value;
                        _xmlDoc.patch.Add(patch);
                        string to = match.Groups[3].Value;
                        _xmlDoc.to.Add(to);
                        Console.WriteLine($"Type: {type}, Patch: {patch}, To: {to}");
                    }
                }
            }
            /**new DeltaModPackageXML
            {
                type = _xmlDoc.type,
                patch = _xmlDoc.patch,
                to = _xmlDoc.to
            };*/
        }
        public void UpdateJSON(Action<DeltaModPackageJSON> updateAction)
        {
            updateAction(_json);
        }
        public void LoadJSON(string? configPath = null)
        {
            configPath ??= _defaultConfigPath;

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var loaded = JsonSerializer.Deserialize<DeltaModPackageJSON>(json);
                    if (loaded != null)
                    {
                        DeltaModPackageJSON = loaded;
                    }
                    for(int i = 0; i < json.Length; i++)
                    {
                        Console.WriteLine(json[i]);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load configuration: {ex.Message}");
                }
                
            }
        }
        public void SaveJSON(string? configPath = null)
        {


            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(DeltaModPackageJSON, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save configuration: {ex.Message}");
            }
        }
        public DeltaModPackageXML LoadFromEnvironment()
        {
            var xml = new DeltaModPackageXML();
            return xml;
        }
    }
    internal class ModPackages
    {

    }
}
