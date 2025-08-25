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
        private readonly SemaphoreSlim _dumpSemaphore;
        private readonly object _dumpLock = new object();

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

            // IMPORTANT: Set to 1 to prevent file conflicts
            // Multiple UTMT instances can't write to the same output directories
            _dumpSemaphore = new SemaphoreSlim(1);
        }

        public async Task<bool> ExecuteDump()
        {
            try
            {
                Console.WriteLine("Starting dump operation.");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                LoadCachedNumbers();

                bool useParallel = ShouldUseParallelDump();

                if (useParallel)
                {
                    Console.WriteLine("Using optimized parallel dump strategy.");
                    await ExecuteOptimizedParallelDump();
                }
                else
                {
                    Console.WriteLine("Using sequential dump for safety.");
                    await ExecuteSequentialDump();
                }

                // new guardrail: check for suspiciously empty mod dumps (exporter fallback already added in the script itself).
                SanityCheckDumpOutputs();

                sw.Stop();
                Console.WriteLine($"Dump completed in {sw.Elapsed.TotalSeconds:F2} seconds");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dump failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private bool ShouldUseParallelDump()
        {
            // Only use parallel if:
            // 1. We have multiple chapters
            // 2. Not too many mods per chapter (to avoid memory issues)
            // 3. Caching is enabled (so we can skip already-dumped files)

            return _config.Config.ChapterAmount > 1 &&
                   _config.Config.ModAmount <= 4 &&
                   _config.Config.CacheEnabled;
        }

        private async Task ExecuteOptimizedParallelDump()
        {
            // Strategy: Process different CHAPTERS in parallel
            // but keep MODS within a chapter sequential to avoid conflicts

            var chapterTasks = new List<Task>();

            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                int capturedChapter = chapter;

                // Each chapter gets its own task
                chapterTasks.Add(Task.Run(async () =>
                {
                    await DumpChapterSequential(capturedChapter);
                }));
            }

            await Task.WhenAll(chapterTasks);
        }

        private async Task ExecuteSequentialDump()
        {
            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                await DumpChapterSequential(chapter);
            }
        }

        private async Task DumpChapterSequential(int chapter)
        {
            var chapterPath = _directoryManager.GetCachePath(
                _config.Config,
                "running",
                $"chapterNumber.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(chapterPath)!);
            File.WriteAllText(chapterPath, chapter.ToString());

            Console.WriteLine($"Dumping chapter {chapter}...");

            // Process mods sequentially within this chapter
            for (int modNumber = 0; modNumber < (_config.Config.ModAmount + 2); modNumber++)
            {
                if (modNumber == 1) continue; // Skip slot 1 (reserved for combined)

                await DumpMod(chapter, modNumber);
            }
        }

        private async Task DumpMod(int chapter, int modNumber)
        {
            // Thread-safe mod number cache update
            lock (_dumpLock)
            {
                File.WriteAllText(
                    _directoryManager.GetCachePath(_config.Config, "running", "modNumbersCache.txt"),
                    modNumber.ToString());

                // Also write chapter number for scripts that need it
                File.WriteAllText(
                    _directoryManager.GetCachePath(_config.Config, "running", "chapterNumber.txt"),
                    chapter.ToString());
            }

            if (modNumber == 1) return;

            var dataWin = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "data.win");

            if (!File.Exists(dataWin))
            {
                Console.WriteLine($"  Chapter {chapter}, Mod {modNumber}: data.win not found, skipping");
                return;
            }

            // Check cache first
            var hash = _hashCache.GetSha1Base64(dataWin);
            var cacheDir = _exportCache.GetDumpCacheDirByHash(chapter, hash, _config.Config);
            var stampPath = _exportCache.GetDumpStampPathByHash(chapter, hash, _config.Config);

            if (_exportCache.IsCacheValid(stampPath, hash, _config.Config))
            {
                Console.WriteLine($"  Chapter {chapter}, Mod {modNumber}: using cache");

                var srcObjects = Path.Combine(cacheDir, "Objects");
                var dstObjects = _directoryManager.GetXDeltaCombinerPath(
                    _config.Config,
                    chapter.ToString(),
                    modNumber.ToString(),
                    "Objects");

                // Use synchronous copy to avoid conflicts
                _exportCache.MirrorObjectsSelective(
                    srcObjects,
                    dstObjects,
                    _config.Config.CacheSpritesEnabled);
                return;
            }

            // Create directory structure
            var objectsPath = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects");

            CreateExportDirectoryStructure(objectsPath);

            // IMPORTANT: Clear any existing files to prevent conflicts
            ClearExistingExports(objectsPath);

            // Run export with semaphore to prevent parallel UTMT executions
            await _dumpSemaphore.WaitAsync();
            try
            {
                Console.WriteLine($"  Chapter {chapter}, Mod {modNumber}: exporting...");

                // Pass the mod number explicitly to help with detection
                await _modTool.RunExportScripts(
                    dataWin,
                    modNumber == 0,          // isVanilla
                    modNumber,
                    _config.Config);

                // Verify AssetOrder.txt location and move if needed
                FixAssetOrderLocation(chapter, modNumber);
            }
            finally
            {
                _dumpSemaphore.Release();
            }

            // Check for warnings
            CheckDumpWarnings(chapter, modNumber);

            // Cache results
            if (_config.Config.CacheEnabled && !string.IsNullOrEmpty(hash))
            {
                try
                {
                    await CacheDumpResults(chapter, modNumber, hash, dataWin);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  WARNING: Failed to cache: {ex.Message}");
                }
            }

            _exportCache.PruneExportCacheIfNeeded(_config.Config);
        }

        private void CreateExportDirectoryStructure(string objectsPath)
        {
            var dirs = new[]
            {
                "CodeEntries", "Sprites", "Backgrounds", "Paths", "Scripts",
                "Shaders", "Fonts", "Timelines", "Objects", "Rooms",
                "Sequences", "AnimCurves", "Sounds", "AudioGroups",
                "Extensions", "TexturePageItems"
            };

            foreach (var dir in dirs)
            {
                Directory.CreateDirectory(Path.Combine(objectsPath, dir));
            }
        }

        private void ClearExistingExports(string objectsPath)
        {
            // Clear existing sprites to prevent conflicts
            var spritesPath = Path.Combine(objectsPath, "Sprites");
            if (Directory.Exists(spritesPath))
            {
                try
                {
                    // Delete all PNG files
                    foreach (var file in Directory.GetFiles(spritesPath, "*.png", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            }

            // Clear code entries
            var codePath = Path.Combine(objectsPath, "CodeEntries");
            if (Directory.Exists(codePath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(codePath, "*.gml", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            }
        }

        private void FixAssetOrderLocation(int chapter, int modNumber)
        {
            // Check if AssetOrder.txt is in the wrong location
            string rootAssetOrder = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "AssetOrder.txt");

            string objectsAssetOrder = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects",
                "AssetOrder.txt");

            if (File.Exists(rootAssetOrder) && !File.Exists(objectsAssetOrder))
            {
                Console.WriteLine($"    Moving AssetOrder.txt to Objects folder");
                Directory.CreateDirectory(Path.GetDirectoryName(objectsAssetOrder)!);
                File.Move(rootAssetOrder, objectsAssetOrder, true);
            }
        }

        private void CheckDumpWarnings(int chapter, int modNumber)
        {
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
                    Console.WriteLine($"    WARNING: {empties.Count} empty GML files found");
                }
            }

            var workAssetOrder = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects",
                "AssetOrder.txt");

            if (!File.Exists(workAssetOrder))
            {
                Console.WriteLine($"    WARNING: AssetOrder.txt missing after dump!");

                // Try to find it in parent directory
                var parentAssetOrder = _directoryManager.GetXDeltaCombinerPath(
                    _config.Config,
                    chapter.ToString(),
                    modNumber.ToString(),
                    "AssetOrder.txt");

                if (File.Exists(parentAssetOrder))
                {
                    Console.WriteLine($"    Found AssetOrder.txt in parent, moving to Objects");
                    File.Move(parentAssetOrder, workAssetOrder, true);
                }
            }
        }

        private async Task CacheDumpResults(int chapter, int modNumber, string hash, string dataWin)
        {
            var postSig = _hashCache.GetSha1Base64(dataWin);
            var outRoot = _exportCache.GetDumpCacheDirByHash(chapter, hash, _config.Config);
            Directory.CreateDirectory(Path.Combine(outRoot, "Objects"));

            var workObjectsRoot = _directoryManager.GetXDeltaCombinerPath(
                _config.Config,
                chapter.ToString(),
                modNumber.ToString(),
                "Objects");

            await Task.Run(() => _exportCache.MirrorObjectsSelective(
                workObjectsRoot,
                Path.Combine(outRoot, "Objects"),
                _config.Config.CacheSpritesEnabled));

            var stampPath = _exportCache.GetDumpStampPathByHash(chapter, hash, _config.Config);
            _exportCache.WriteStamp(stampPath, hash, postSig);
        }

        // Add batch processing for better performance
        public async Task<bool> ExecuteDumpBatched()
        {
            try
            {
                Console.WriteLine("Starting batched dump operation...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                LoadCachedNumbers();

                // Group mods that can be processed together
                var batches = CreateDumpBatches();

                foreach (var batch in batches)
                {
                    Console.WriteLine($"Processing batch with {batch.Count} items...");

                    // Process each item in the batch sequentially
                    foreach (var item in batch)
                    {
                        await DumpMod(item.Chapter, item.ModNumber);
                    }
                }

                sw.Stop();
                Console.WriteLine($"Batched dump completed in {sw.Elapsed.TotalSeconds:F2} seconds");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batched dump failed: {ex.Message}");
                return false;
            }
        }

        private List<List<(int Chapter, int ModNumber)>> CreateDumpBatches()
        {
            var batches = new List<List<(int Chapter, int ModNumber)>>();
            var currentBatch = new List<(int Chapter, int ModNumber)>();

            // Create batches that don't conflict
            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                for (int modNumber = 0; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    if (modNumber == 1) continue;

                    currentBatch.Add((chapter, modNumber));

                    // Create a new batch every N items to manage memory
                    if (currentBatch.Count >= 5)
                    {
                        batches.Add(new List<(int, int)>(currentBatch));
                        currentBatch.Clear();
                    }
                }
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);

            return batches;
        }


        public async Task<bool> ExecuteMassPatch(string[] patchPaths)
        {
            try
            {
                Console.WriteLine("Starting mass patch operation...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                _directoryManager.CreateCombinerDirectories(_config.Config);
                await CopyVanillaFiles();
                await _patchService.ApplyPatches(patchPaths, _config.Config);

                sw.Stop();
                Console.WriteLine($"Mass patch completed in {sw.Elapsed.TotalSeconds:F2} seconds");
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
                Console.WriteLine("Starting compare and combine operation.");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                LoadCachedNumbers();
                CreateModifiedList();
                await _modCombiner.CompareCombine(_config.Config);
                SaveModifiedAssets();
                _config.UpdateConfiguration(c => c.Combined = true);

                sw.Stop();
                Console.WriteLine($"Compare and combine completed in {sw.Elapsed.TotalSeconds:F2} seconds");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compare and combine failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteImport()
        {
            try
            {
                Console.WriteLine("Starting import operation.");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                LoadCachedNumbers();

                if (!ShouldImport())
                {
                    Console.WriteLine("No changes detected, skipping import");
                    return true;
                }

                // Ensure NewObjects stubs and graphics are applied first (same order UTMT scripts expect),
                // then run ImportGML with capture to fail fast on compile errors.
                for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
                {
                    var chapterRoot = _directoryManager.GetXDeltaCombinerPath(
                        _config.Config, chapter.ToString(), "1", "");
                    var dataWin = Path.Combine(chapterRoot, "data.win");

                    // Skip if this chapter has no combined work
                    if (!File.Exists(dataWin))
                        continue;

                    // Let the ModCombiner prepare/create any NewObjects files first.
                    await _modCombiner.HandleNewObjects(_config.Config);

                    // Run graphics + new objects first (if present).
                    var importList = BuildPresentImportList(chapterRoot, includeCode:false);
                    if (importList.Count > 0)
                        await _modTool.RunImportScripts(dataWin, importList.ToArray(), _config.Config);

                    // Fail-fast probe: ImportGML with capture.
                    var gmlResult = await _modTool.RunScriptAndCapture(dataWin, "ImportGML.csx", _config.Config);
                    if (!gmlResult.Succeeded)
                    {
                        Console.WriteLine("===== ImportGML probe failed (compile/linking) =====");
                        Console.WriteLine(gmlResult.StdOut);
                        Console.WriteLine(gmlResult.StdErr);
                        Console.WriteLine("Aborting import for safety.");
                        return false;
                    }

                    // AssetOrder (optional)
                    if (HasAssetOrder(chapterRoot))
                        await _modTool.RunScript(dataWin, "ImportAssetOrder.csx", _config.Config);
                }

                sw.Stop();
                Console.WriteLine($"Import completed in {sw.Elapsed.TotalSeconds:F2} seconds");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import failed: {ex.Message}");
                return false;
            }
        }

        private bool ShouldImport()
        {
            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                var stampFile = Path.Combine(
                    _directoryManager.GetCachePath(_config.Config, "running"),
                    $"chapter_{chapter}_changes.txt");

                if (File.Exists(stampFile))
                {
                    var parts = File.ReadAllText(stampFile).Split('|');
                    if (parts.Length > 0 && int.TryParse(parts[0], out int changes) && changes > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
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

        private async Task CopyVanillaFiles()
        {
            var vanillaFiles = _directoryManager.FindDataWinFiles(_config.Config.VanillaPath!);
            _config.UpdateConfiguration(c => c.ChapterAmount = vanillaFiles.Count);
            SaveCachedNumbers();

            var tasks = new List<Task>();
            for (int chapter = 0; chapter < vanillaFiles.Count; chapter++)
            {
                for (int modNumber = 0; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    int capturedChapter = chapter;
                    int capturedMod = modNumber;
                    string sourceFile = vanillaFiles[chapter];

                    tasks.Add(Task.Run(() =>
                    {
                        var targetPath = _directoryManager.GetXDeltaCombinerPath(
                            _config.Config,
                            capturedChapter.ToString(),
                            capturedMod.ToString(),
                            "data.win");

                        _fileLinker.LinkOrCopy(sourceFile, targetPath);
                    }));
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task CreateChapterResult(int chapter, string modName)
        {
            var resultPath = Path.Combine(_config.Config.OutputPath!, "result", modName);

            if (_config.Config.Combined)
            {
                var chapterResultPath = Path.Combine(resultPath, chapter.ToString());

                await _patchService.CreatePatch(
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "0", "data.win"),
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "1", "data.win"),
                    Path.Combine(resultPath, $"{modName}-Chapter{chapter}.xdelta"),
                    _config.Config);

                _fileLinker.LinkOrCopy(
                    _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "1", "data.win"),
                    Path.Combine(chapterResultPath, "data.win"));

                CopyModifiedAssetsList(chapter, chapterResultPath);
            }
            else
            {
                for (int modNumber = 2; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    await CreateModResult(chapter, modNumber, resultPath);
                }
            }
        }

        private async Task CreateModResult(int chapter, int modNumber, string resultPath)
        {
            var modResultPath = Path.Combine(resultPath, chapter.ToString(), modNumber.ToString());

            await _patchService.CreatePatch(
                _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), "0", "data.win"),
                _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), modNumber.ToString(), "data.win"),
                Path.Combine(resultPath, chapter.ToString(), $"{modNumber}.xdelta"),
                _config.Config);

            _fileLinker.LinkOrCopy(
                _directoryManager.GetXDeltaCombinerPath(_config.Config, chapter.ToString(), modNumber.ToString(), "data.win"),
                Path.Combine(modResultPath, "data.win"));
        }

        private void LoadCachedNumbers()
        {
            var chapterAmountFile = _directoryManager.GetCachePath(
                _config.Config, "running", "chapterAmount.txt");

            if (File.Exists(chapterAmountFile))
            {
                if (int.TryParse(File.ReadAllText(chapterAmountFile), out var chapters))
                {
                    _config.UpdateConfiguration(c => c.ChapterAmount = chapters);
                }
            }

            var modAmountFile = _directoryManager.GetCachePath(
                _config.Config, "running", "modAmount.txt");

            if (File.Exists(modAmountFile))
            {
                if (int.TryParse(File.ReadAllText(modAmountFile), out var mods))
                {
                    _config.UpdateConfiguration(c => c.ModAmount = mods);
                }
            }
        }

        private void SaveCachedNumbers()
        {
            var chapterAmountFile = _directoryManager.GetCachePath(
                _config.Config, "running", "chapterAmount.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(chapterAmountFile)!);
            File.WriteAllText(chapterAmountFile, _config.Config.ChapterAmount.ToString());

            var modAmountFile = _directoryManager.GetCachePath(
                _config.Config, "running", "modAmount.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(modAmountFile)!);
            File.WriteAllText(modAmountFile, _config.Config.ModAmount.ToString());
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

        private static bool DirHasAny(string dir, string searchPattern, SearchOption opt = SearchOption.AllDirectories)
            => Directory.Exists(dir) && Directory.EnumerateFiles(dir, searchPattern, opt).Any();

        private static bool ObjectsTreeLooksEmpty(string slotRoot)
        {
            // Always inspect under Objects/
            var objectsDir = Path.Combine(slotRoot, "Objects");
            var codeDir    = Path.Combine(objectsDir, "CodeEntries");
            var spritesDir = Path.Combine(objectsDir, "Sprites");
            var newObjDir  = Path.Combine(objectsDir, "NewObjects");

            int files =
                (DirHasAny(codeDir, "*.gml")  ? Directory.EnumerateFiles(codeDir,   "*.gml",  SearchOption.AllDirectories).Count() : 0) +
                (DirHasAny(spritesDir, "*.png")? Directory.EnumerateFiles(spritesDir,"*.png",  SearchOption.AllDirectories).Count() : 0) +
                (DirHasAny(newObjDir, "*.*")   ? Directory.EnumerateFiles(newObjDir, "*.*",   SearchOption.AllDirectories).Count() : 0);

            // Allow one file (e.g., a single stub) before calling it empty
            return files <= 1;
        }

        void SanityCheckDumpOutputs()
        {
            for (int chapter = 0; chapter < _config.Config.ChapterAmount; chapter++)
            {
                for (int modNumber = 2; modNumber < (_config.Config.ModAmount + 2); modNumber++)
                {
                    // Slot root: .../xDeltaCombiner/<chapter>/<slot>/
                    var slotRoot = _directoryManager.GetXDeltaCombinerPath(
                        _config.Config, chapter.ToString(), modNumber.ToString(), "");

                    // Where we actually expect files:
                    var objectsDir  = Path.Combine(slotRoot, "Objects");
                    var spritesDir  = Path.Combine(objectsDir, "Sprites");
                    var codeDir     = Path.Combine(objectsDir, "CodeEntries");
                    var newObjsDir  = Path.Combine(objectsDir, "NewObjects");
                    var assetOrder  = Path.Combine(objectsDir, "AssetOrder.txt");

                    bool looksEmpty = ObjectsTreeLooksEmpty(slotRoot);
                    if (looksEmpty)
                    {
                        Console.WriteLine(
                            $"[Sanity] Chapter {chapter}, Mod {modNumber - 1}: " +
                            $"Objects tree looks empty.\n" +
                            $"         Checked: {codeDir}, {spritesDir}, {newObjsDir}");
                    }

                    // Only check for *file* AssetOrder.txt, by design.
                    if (!HasAssetOrder(slotRoot))
                    {
                        Console.WriteLine(
                            $"[Sanity] Chapter {chapter}, Mod {modNumber - 1}: AssetOrder.txt not present at {assetOrder}");
                    }
                }
            }
        }


        private static bool HasAssetOrder(string slotRoot)
        {
            var file = Path.Combine(slotRoot, "Objects", "AssetOrder.txt");
            return File.Exists(file);
        }

        private static int CountFilesIfExists(string dir, string pattern = "*")
        {
            return Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).Count()
                : 0;
        }

        private static bool LooksEmpty(string slotRoot)
        {
            bool objectsEmpty = !Directory.Exists(Path.Combine(slotRoot, "Objects")) ||
                                !Directory.EnumerateFiles(Path.Combine(slotRoot, "Objects"), "*", SearchOption.AllDirectories).Any();

            bool spritesEmpty = !Directory.Exists(Path.Combine(slotRoot, "Sprites")) ||
                                !Directory.EnumerateFiles(Path.Combine(slotRoot, "Sprites"), "*", SearchOption.AllDirectories).Any();

            return objectsEmpty && spritesEmpty;
        }

        private static List<string> BuildPresentImportList(string chapterRoot, bool includeCode)
        {
            var list = new List<string>();

            // ImportGraphics expects sprites/atlases exported by our scripts
            if (Directory.Exists(Path.Combine(chapterRoot, "Sprites")) ||
                Directory.Exists(Path.Combine(chapterRoot, "TexturesGrouped")) ||
                Directory.Exists(Path.Combine(chapterRoot, "Graphics")))
            {
                list.Add("ImportGraphics.csx");
            }

            // New objects (object definitions / asset order deltas)
            if (Directory.Exists(Path.Combine(chapterRoot, "NewObjects")))
            {
                list.Add("ImportNewObjects.csx");
            }

            if (includeCode)
                list.Add("ImportGML.csx");

            return list;
        }
    }
}