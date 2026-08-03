using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace GM3P.Data
{
    public interface IDeltaModPackageService
    {
        GM3PConfig Config { get; }
        void LoadConfiguration(string? configPath = null);
        void SaveConfiguration(string? configPath = null);
    }
    public class DeltaModPackageJSON
    {
        public Dictionary<string, DeltaModPackagemetadata>? metadata { get; set; }
        public Dictionary<string, DeltaModPackageneededFiles>? neededFiles { get; set; }
        public string deltaruneTargetVersion { get; set; } = "1.0.0";
        public Dictionary<string, DeltaModPackageExporter>? exporter { get; set; }
    }
    public class DeltaModPackagemetadata {
        public string name { get; set; } = "My Mod";
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
    }
    public class DeltaModPackageService : IDeltaModPackageService
    {
        private GM3PConfig _config;
        private readonly string _defaultConfigPath;
        public DeltaModPackageJSON DeltaModPackageJSON { get; set; } = new DeltaModPackageJSON();
        public GM3PConfig Config => _config;
        public DeltaModPackageService()
        {
            var pwd = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            _defaultConfigPath = Path.Combine(pwd, "deltamodpackage.json");
            _config = GM3PConfig.LoadFromEnvironment();
        }
        public void LoadConfiguration(string? configPath = null)
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load configuration: {ex.Message}");
                }
            }
        }
        public void SaveConfiguration(string? configPath = null)
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
    }
    internal class ModPackages
    {

    }
}
