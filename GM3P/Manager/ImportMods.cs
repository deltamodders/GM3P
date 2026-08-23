using GM3P.Data;
using System;
using System.Collections.Generic;
using System.Text;
using GM3P.FileSystem;

namespace GM3P.Manager
{
    public interface IInstall
    {
        Task<bool> InstallMods(string patchFile, GM3PConfig config);
        Task InstallInstance(string modName, string gamePath, string game, string version, GM3PConfig config);
    }
    // This class is responsible for importing mods into the mod manager. It is called "Install" to avoid confusion with the "ExecuteImport()" Task and similarly named varibles and tasks in "UndertaleModTool.cs" and "GM3POrchestrator.cs", which runs the import scripts on legacy UTMT.
    public class Install: IInstall
    {
        private readonly IDirectoryManager _directoryManager;
        private readonly IDeltaModPackageService _deltamodPackageService;
        public Install(IDirectoryManager directoryManager)
        {
            _directoryManager = directoryManager;
        }
        public async Task<bool> InstallMods(string patchFile, GM3PConfig config)
        {
            // Implementation for importing mods goes here
            if (!File.Exists(patchFile))
            {
                return false;
            }
            Directory.CreateDirectory(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "temp"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips"));
            string zip = Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "zips", Path.GetFileName(patchFile));
            File.Copy(patchFile, zip, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "temp"), true);
            string modName;
            Console.WriteLine(1);
            try 
            { 
                _deltamodPackageService.LoadJSON(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "temp", "meta.json"));
            
                if (_deltamodPackageService.DeltaModPackagemetadata.packageID != null)
                {
                    modName = _deltamodPackageService.DeltaModPackagemetadata.packageID;
                }
                else
                {
                    modName = _deltamodPackageService.DeltaModPackagemetadata.name;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"The mod name null or can't be extracted because of {ex}. Using zip name.");
                modName = Path.GetFileNameWithoutExtension(zip);
            }
            Console.WriteLine(3);
            await _directoryManager.CopyDirectory(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "temp"), Path.Combine(config.OutputPath!, "DeltamodLite", "mods", modName), true);
            Directory.Delete(Path.Combine(config.OutputPath!, "DeltamodLite", "mods", "temp"),true);
            return true;
        }
        public async Task InstallInstance(string modName, string gamePath, string game, string version, GM3PConfig config)
        {
            // Implementation for importing vanilla files goes here
            try
            {
                await _directoryManager.CopyDirectory(gamePath, Path.Combine(config.OutputPath!,"DeltamodLite","instances",modName,game,version,game) , true);
            }
            catch (Exception ex) {
                Console.WriteLine($"Error copying vanilla files for mod {modName}: {ex.Message}");

            }
        }
    }
}
