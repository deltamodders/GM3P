using GM3P.Cache;
using GM3P.Data;
using GM3P.FileSystem;
using GM3P.GameMaker;
using GM3P.Merging;
using GM3P.Patching;

namespace GM3P.Core
{
    public interface IGM3POrchestrator
    {
        Task<bool> ExecuteMassPatch(string[] patchPaths);
        Task<bool> ExecuteCompareCombine();
        Task<bool> ExecuteResult(string modName);
        Task<bool> ExecuteDump();
        Task<bool> ExecuteImport();
        void Clear(string target = "runningCache");
    }

    public class GM3POrchestrator : IGM3POrchestrator
    {
        private readonly IConfigurationService _config;
        private readonly IDirectoryManager _directoryManager;
        private readonly IFileLinker _fileLinker;
        private readonly IHashCache _hashCache;
        private readonly IExportCache _exportCache;
        private readonly IPatchService _patchService;
        private readonly IModCombiner _modCombiner;
        private readonly IUndertaleModTool _modTool;

        public GM3POrchestrator(
            IConfigurationService config,
            IDirectoryManager directoryManager,
            IFileLinker fileLinker,
            IHashCache hashCache,
            IExportCache exportCache,
            IPatchService patchService,
            IModCombiner modCombiner,
            IUndertaleModTool modTool)
        {
            _config = config;
            _directoryManager = directoryManager;
            _fileLinker = fileLinker;
            _hashCache = hashCache;
            _exportCache = exportCache;
            _patchService = patchService;
            _modCombiner = modCombiner;
            _modTool = modTool;
        }

        public async Task<bool> ExecuteMassPatch(string[] patchPaths)
        {
            try
            {
                Console.WriteLine("Starting mass patch operation...");

                // Setup directories
                _directoryManager.CreateCombinerDirectories(_config.Config);

                // Copy vanilla files
                await CopyVanillaFiles();

                // Apply patches
                await _patchService.ApplyPatches(patchPaths, _config.Config);

                Console.WriteLine("Mass patch completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mass patch failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteCompareCombine()
        {
            try
            {
                Console.WriteLine("Starting compare and combine operation...");

                // Load cached numbers if available
                LoadCachedNumbers();

                // Create modified assets list
                CreateModifiedList();

                // Execute combination
                await _modCombiner.CompareCombine(_config.Config);

                // Save modified assets
                SaveModifiedAssets();

                _config.UpdateConfiguration(c => c.Combined = true);

                Console.WriteLine("Compare and combine completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compare and combine failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteDump()
        {
            try
            {
                Console.WriteLine("Starting dump operation...");
                LoadCachedNumbers();

                for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
                {
                    await DumpChapter(chapter);
                }

                Console.WriteLine("Dump completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dump failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteImport()
        {
            try
            {
                Console.WriteLine("Starting import operation...");
                LoadCachedNumbers();

                await _modCombiner.HandleNewObjects(_config.Config);
                await _modCombiner.ImportWithNewObjects(_config.Config);

                Console.WriteLine("Import completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteResult(string modName)
        {
            try
            {
                Console.WriteLine($"Creating result for {modName}...");
                LoadCachedNumbers();

                _directoryManager.CreateResultDirectories(_config.Config, modName);

                for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
                {
                    await CreateChapterResult(chapter, modName);
                }

                Console.WriteLine($"Result created successfully for {modName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Result creation failed: {ex.Message}");
                return false;
            }
        }

        public void Clear(string target = "runningCache")
        {
            var outputPath = _config.Config.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
                return;

            switch (target.ToLower())
            {
                case "runningcache":
                    _directoryManager.ClearDirectory(Path.Combine(outputPath, "xDeltaCombiner"));
                    _directoryManager.ClearDirectory(Path.Combine(outputPath, "Cache", "running"));
                    break;

                case "cache":
                    _directoryManager.ClearDirectory(Path.Combine(outputPath, "Cache"));
                    _hashCache.Clear();
                    break;

                case "output":
                    _directoryManager.ClearDirectory(outputPath);
                    break;

                case "modpacks":
                    _directoryManager.ClearDirectory(Path.Combine(outputPath, "result"));
                    break;

                default:
                    Console.WriteLine($"Unknown clear target: {target}");
                    break;
            }
        }

        private async Task CopyVanillaFiles()
        {
            var vanillaFiles = _directoryManager.FindDataWinFiles(_config.Config.VanillaPath!);
            _config.UpdateConfiguration(c => c.ChapterAmount = vanillaFiles.Count);

            SaveCachedNumbers();

            for (int chapter = 0; chapter < vanillaFiles.Count; chapter++)
            {
                for (int modNumber = 0; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    var targetPath = _directoryManager.GetXDeltaCombinerPath(
                        _config.Config,
                        chapter.ToString(),
                        modNumber.ToString(),
                        "data.win");

                    _fileLinker.LinkOrCopy(vanillaFiles[chapter], targetPath);
                }
            }
        }

        private async Task DumpChapter(int chapter)
        {
            var chapterPath = _directoryManager.GetCachePath(
                _config.Config,
                "running",
                "chapterNumber.txt");
            File.WriteAllText(chapterPath, chapter.ToString());

            Console.WriteLine($"Dumping chapter {chapter}...");

            for (int modNumber = 0; modNumber < (_config.Config.ModAmount + 2); modNumber++)
            {
                if (modNumber == 1) continue; // Skip slot 1 (reserved for combined)

                await DumpMod(chapter, modNumber);
            }
        }

        private async Task DumpMod(int chapter, int modNumber)
        {
            // Write mod number to cache file like original
            File.WriteAllText(
                _directoryManager.GetCachePath(_config.Config, "running", "modNumbersCache.txt"),
                modNumber.ToString());

            if (modNumber == 1) return; // Skip slot 1 like original

            var dataWin = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "data.win");

            if (!File.Exists(dataWin))
                return;

            // Check cache EXACTLY like original
            var hash = _hashCache.GetSha1Base64(dataWin);
            var cacheDir = _exportCache.GetDumpCacheDirByHash(chapter, hash, _config.Config);
            var stampPath = _exportCache.GetDumpStampPathByHash(chapter, hash, _config.Config);

            if (_exportCache.IsCacheValid(stampPath, hash, _config.Config))
            {
                Console.WriteLine($"  Mod {modNumber}: unchanged since last dump, reusing cached export");

                var srcObjects = Path.Combine(cacheDir, "Objects");
                var dstObjects = _directoryManager.GetXDeltaCombinerPath(
                    _config.Config,
                    chapter.ToString(),
                    modNumber.ToString(),
                    "Objects");

                _exportCache.MirrorObjectsSelective(
                    srcObjects,
                    dstObjects,
                    _config.Config.CacheSpritesEnabled);
                return;
            }

            // No cache - run UTMT EXACTLY like original
            await _modTool.RunExportScripts(dataWin, _config.Config);

            // Check for warnings like original
            var workCodes = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects",
                "CodeEntries");

            if (Directory.Exists(workCodes))
            {
                var empties = Directory.EnumerateFiles(workCodes, "*.gml")
                                       .Select(f => new FileInfo(f))
                                       .Where(fi => fi.Length == 0)
                                       .ToList();
                if (empties.Count > 0)
                {
                    Console.WriteLine($"  WARNING: {empties.Count} empty GML files found in Mod {modNumber - 1}:");
                    foreach (var ef in empties.Take(5))
                        Console.WriteLine($"    - {Path.GetFileName(ef.FullName)}");
                }
            }

            var workAssetOrder = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects",
                "AssetOrder.txt");

            if (!File.Exists(workAssetOrder))
                Console.WriteLine("  WARNING: AssetOrder.txt missing after dump");

            // Cache results like original
            if (_config.Config.CacheEnabled && !string.IsNullOrEmpty(hash))
            {
                try
                {
                    var postSig = _hashCache.GetSha1Base64(dataWin);
                    var outRoot = _exportCache.GetDumpCacheDirByHash(chapter, hash, _config.Config);
                    Directory.CreateDirectory(Path.Combine(outRoot, "Objects"));

                    var workObjectsRoot = _directoryManager.GetXDeltaCombinerPath(
                        _config.Config,
                        chapter.ToString(),
                        modNumber.ToString(),
                        "Objects");

                    _exportCache.MirrorObjectsSelective(
                        workObjectsRoot,
                        Path.Combine(outRoot, "Objects"),
                        _config.Config.CacheSpritesEnabled);

                    _exportCache.WriteStamp(stampPath, hash, postSig);
                }
                catch { }
            }

            _exportCache.PruneExportCacheIfNeeded(_config.Config);
        }

        private async Task CreateChapterResult(int chapter, string modName)
        {
            var resultPath = Path.Combine(
                _config.Config.OutputPath!,
                "result",
                modName);

            if (_config.Config.Combined)
            {
                // Create single combined result
                var chapterResultPath = Path.Combine(resultPath, chapter.ToString());

                // Create xDelta patch
                await _patchService.CreatePatch(
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "0", "data.win"),
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "1", "data.win"),
                    Path.Combine(resultPath, $"{modName}-Chapter{chapter}.xdelta"),
                    _config.Config);

                // Copy data.win
                _fileLinker.LinkOrCopy(
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "1", "data.win"),
                    Path.Combine(chapterResultPath, "data.win"));

                // Copy modified assets list
                CopyModifiedAssetsList(chapter, chapterResultPath);
            }
            else
            {
                // Create individual mod results
                for (int modNumber = 2; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    var modResultPath = Path.Combine(resultPath, chapter.ToString(), modNumber.ToString());

                    // Create xDelta patch
                    await _patchService.CreatePatch(
                        _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "0", "data.win"),
                        _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), modNumber.ToString(), "data.win"),
                        Path.Combine(resultPath, chapter.ToString(), $"{modNumber}.xdelta"),
                        _config.Config);

                    // Copy data.win
                    _fileLinker.LinkOrCopy(
                        _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), modNumber.ToString(), "data.win"),
                        Path.Combine(modResultPath, "data.win"));
                }
            }
        }

        private void LoadCachedNumbers()
        {
            var chapterAmountFile = _directoryManager.GetCachePath(
                _config.Config,
                "running",
                "chapterAmount.txt");

            if (File.Exists(chapterAmountFile))
            {
                if (int.TryParse(File.ReadAllText(chapterAmountFile), out var chapters))
                {
                    _config.UpdateConfiguration(c => c.ChapterAmount = chapters);
                }
            }
        }

        private void SaveCachedNumbers()
        {
            var chapterAmountFile = _directoryManager.GetCachePath(
                _config.Config,
                "running",
                "chapterAmount.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(chapterAmountFile)!);
            File.WriteAllText(chapterAmountFile, _config.Config.ChapterAmount.ToString());
        }

        private void CreateModifiedList()
        {
            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                var path = _directoryManager.GetXDeltaCombinerPath(
                    _config.Config,
                    chapter.ToString(),
                    "1",
                    "modifiedAssets.txt");

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Create(path).Close();
            }
        }

        private void SaveModifiedAssets()
        {
            var modifiedAssets = _modCombiner.GetModifiedAssets();

            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                var path = _directoryManager.GetXDeltaCombinerPath(
                    _config.Config,
                    chapter.ToString(),
                    "1",
                    "modifiedAssets.txt");

                File.WriteAllLines(path, modifiedAssets);
            }
        }

        private void CopyModifiedAssetsList(int chapter, string destinationPath)
        {
            var srcPath = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                "1",
                "modifiedAssets.txt");

            if (File.Exists(srcPath))
            {
                var dstPath = Path.Combine(destinationPath, "modifiedAssets.txt");
                File.Copy(srcPath, dstPath, true);
            }
        }
    }
}