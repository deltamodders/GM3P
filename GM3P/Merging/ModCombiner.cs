// ModCombiner.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GM3P.Cache;
using GM3P.Data;
using GM3P.FileSystem;
using GM3P.GameMaker;

namespace GM3P.Merging
{
    // Result class to avoid ref parameters in async method
    public class ProcessFileResult
    {
        public int ChangedCount { get; set; }
        public bool CodeChanged { get; set; }
        public bool SpriteChanged { get; set; }
    }

    public interface IModCombiner
    {
        Task CompareCombine(GM3PConfig config);
        Task HandleNewObjects(GM3PConfig config);
        Task ImportWithNewObjects(GM3PConfig config);
        List<string> GetModifiedAssets();
    }

    public class ModCombiner : IModCombiner
    {
        private readonly IDirectoryManager _directoryManager;
        private readonly IFileLinker _fileLinker;
        private readonly IHashCache _hashCache;
        private readonly IPngUtils _pngUtils;
        private readonly IAssetHelper _assetHelper;
        private readonly IAssetOrderMerger _assetOrderMerger;
        private readonly IGitService _gitService;
        private readonly IUndertaleModTool _modTool;

        private readonly List<string> _modifiedAssets = new List<string>
        {
            "Asset Name                       Hash (SHA1 in Base64)"
        };

        public ModCombiner(
            IDirectoryManager directoryManager,
            IFileLinker fileLinker,
            IHashCache hashCache,
            IPngUtils pngUtils,
            IAssetHelper assetHelper,
            IAssetOrderMerger assetOrderMerger,
            IGitService gitService,
            IUndertaleModTool modTool)
        {
            _directoryManager = directoryManager;
            _fileLinker = fileLinker;
            _hashCache = hashCache;
            _pngUtils = pngUtils;
            _assetHelper = assetHelper;
            _assetOrderMerger = assetOrderMerger;
            _gitService = gitService;
            _modTool = modTool;
        }

        public async Task CompareCombine(GM3PConfig config)
        {
            for (int chapter = 0; chapter < config.ChapterAmount; chapter++)
            {
                Console.WriteLine(chapter == 0 ? "Processing Root Chapter:" : $"Processing Chapter {chapter}:");

                int changedThisChapter = 0;
                bool anyCodeChanged = false;
                bool anySpriteChanged = false;
                bool assetOrderChanged = false;

                var chapterModified = new List<string>();

                string vanillaObjectsPath = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "0", "Objects");

                if (!Directory.Exists(vanillaObjectsPath))
                {
                    Console.WriteLine($"  WARNING: No vanilla Objects folder for chapter {chapter}");
                    continue;
                }

                // Clear stale merged files
                string mergedObjectsPath = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "1", "Objects");

                if (Directory.Exists(mergedObjectsPath))
                {
                    try { Directory.Delete(mergedObjectsPath, true); } catch { }
                }
                Directory.CreateDirectory(mergedObjectsPath);

                var vanillaFiles = Directory.GetFiles(vanillaObjectsPath, "*", SearchOption.AllDirectories);
                Console.WriteLine($"  Found {vanillaFiles.Length} vanilla files");

                // Build file dictionaries
                var vanillaFileDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var allFileVersions = new Dictionary<string, List<ModFileInfo>>(StringComparer.OrdinalIgnoreCase);
                var allKnown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add vanilla versions
                foreach (var vf in vanillaFiles)
                {
                    string relKey = _assetHelper.NormalizeKey(Path.GetRelativePath(vanillaObjectsPath, vf));
                    vanillaFileDict[relKey] = vf;
                    allKnown.Add(relKey);

                    if (!allFileVersions.TryGetValue(relKey, out var list))
                    {
                        list = new List<ModFileInfo>();
                        allFileVersions[relKey] = list;
                    }
                    list.Add(new ModFileInfo { ModNumber = 0, FilePath = vf, ModName = "Vanilla" });
                }

                // Add mod versions
                for (int modNumber = 2; modNumber < (config.ModAmount + 2); modNumber++)
                {
                    string modObjectsPath = _directoryManager.GetXDeltaCombinerPath(
                        config, chapter.ToString(), modNumber.ToString(), "Objects");

                    if (!Directory.Exists(modObjectsPath)) continue;

                    var modFiles = Directory.GetFiles(modObjectsPath, "*", SearchOption.AllDirectories);
                    Console.WriteLine($"  Mod {modNumber - 1} has {modFiles.Length} files");

                    foreach (var mf in modFiles)
                    {
                        string relKey = _assetHelper.NormalizeKey(Path.GetRelativePath(modObjectsPath, mf));
                        allKnown.Add(relKey);

                        if (!allFileVersions.TryGetValue(relKey, out var list))
                        {
                            list = new List<ModFileInfo>();
                            allFileVersions[relKey] = list;
                        }
                        list.Add(new ModFileInfo { ModNumber = modNumber, FilePath = mf, ModName = $"Mod {modNumber - 1}" });
                    }
                }

                Console.WriteLine($"Chapter {chapter}: Found {allKnown.Count} unique files across vanilla and {config.ModAmount} mod(s)");

                // Process all files except AssetOrder.txt
                foreach (string relKey in allKnown)
                {
                    if (relKey.Equals("assetorder.txt", StringComparison.OrdinalIgnoreCase)) continue;

                    var result = await ProcessFile(relKey, allFileVersions[relKey], vanillaFileDict,
                                                  mergedObjectsPath, chapterModified);

                    changedThisChapter += result.ChangedCount;
                    anyCodeChanged = anyCodeChanged || result.CodeChanged;
                    anySpriteChanged = anySpriteChanged || result.SpriteChanged;
                }

                // Handle AssetOrder.txt
                _assetOrderMerger.HandleAssetOrderFile(chapter, allFileVersions, vanillaFileDict, config);

                // Check if AssetOrder.txt changed
                var mergedAO = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "1", "Objects", "AssetOrder.txt");
                if (File.Exists(mergedAO))
                {
                    var vanillaAO = _directoryManager.GetXDeltaCombinerPath(
                        config, chapter.ToString(), "0", "Objects", "AssetOrder.txt");
                    if (!File.Exists(vanillaAO) || !FilesEqual(mergedAO, vanillaAO))
                    {
                        assetOrderChanged = true;
                        changedThisChapter++;
                    }
                }

                // Validate sprites
                ValidateSprites(chapter, config);

                // Save change stamps
                SaveChangeStamp(chapter, changedThisChapter, anyCodeChanged, anySpriteChanged, assetOrderChanged, config);

                // Write modified assets list
                var modListPath = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "1", "modifiedAssets.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(modListPath)!);
                File.WriteAllLines(modListPath, chapterModified);
                Console.WriteLine($"\nChapter {chapter} Summary:");
                Console.WriteLine($"  Total files processed: {allKnown.Count}");
                Console.WriteLine($"  Files changed: {changedThisChapter}");
                Console.WriteLine($"  Code changes: {(anyCodeChanged ? "Yes" : "No")}");
                Console.WriteLine($"  Sprite changes: {(anySpriteChanged ? "Yes" : "No")}");
                Console.WriteLine($"  AssetOrder changes: {(assetOrderChanged ? "Yes" : "No")}");

                if (changedThisChapter == 0)
                {
                    Console.WriteLine("  WARNING: No changes detected! Check if mods were properly dumped.");
                }
            }
        }

        private async Task<ProcessFileResult> ProcessFile(string relKey, List<ModFileInfo> versions,
                                                         Dictionary<string, string> vanillaFileDict,
                                                         string mergedObjectsPath, List<string> chapterModified)
        {
            var result = new ProcessFileResult();

            var vanillaVersion = versions.FirstOrDefault(v => v.ModNumber == 0);
            var modVersions = versions.Where(v => v.ModNumber > 0).ToList();

            // Compute differences
            var different = ComputeDifferences(vanillaVersion, modVersions, relKey);

            if (different.Count == 0) return result; // No changes

            string targetPath = Path.Combine(mergedObjectsPath, relKey);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (different.Count == 1)
            {
                // Single mod changed it
                File.Copy(different[0].FilePath, targetPath, true);
            }
            else
            {
                // Multiple mods changed it
                await MergeMultipleVersions(relKey, different, vanillaVersion, targetPath);
            }

            // Track changes
            string hash = _hashCache.GetSha1Base64(targetPath);
            _modifiedAssets.Add($"{relKey}        {hash}");
            chapterModified.Add($"{relKey}        {hash}");

            result.ChangedCount = 1;

            string ext = Path.GetExtension(relKey);
            if (relKey.StartsWith("sprites/", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                result.SpriteChanged = true;
            }

            if (relKey.Contains("/codeentries/", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".gml", StringComparison.OrdinalIgnoreCase))
            {
                result.CodeChanged = true;
            }

            return result;
        }

        private List<ModFileInfo> ComputeDifferences(ModFileInfo? vanillaVersion, List<ModFileInfo> modVersions, string relKey)
        {
            var different = new List<ModFileInfo>();

            if (vanillaVersion != null && modVersions.Count > 0)
            {
                string ext = Path.GetExtension(relKey);
                if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var mv in modVersions)
                    {
                        if (_pngUtils.AreSpritesDifferent(mv.FilePath, vanillaVersion.FilePath))
                        {
                            different.Add(mv);
                            Console.WriteLine($"    Sprite different: {Path.GetFileName(mv.FilePath)} from {mv.ModName}");
                        }
                    }
                }
                else
                {
                    string vHash = _hashCache.GetSha1Base64(vanillaVersion.FilePath);
                    foreach (var mv in modVersions)
                    {
                        string mHash = _hashCache.GetSha1Base64(mv.FilePath);
                        if (!string.Equals(vHash, mHash, StringComparison.Ordinal))
                        {
                            different.Add(mv);
                            // Debug output to see what's being detected
                            if (ext.Equals(".gml", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"    Code different: {Path.GetFileName(mv.FilePath)} from {mv.ModName}");
                            }
                        }
                    }
                }
            }
            else if (vanillaVersion == null && modVersions.Count > 0)
            {
                // New file from mods
                different = modVersions;
                Console.WriteLine($"    New file from mods: {Path.GetFileName(modVersions[0].FilePath)}");
            }

            return different;
        }

        private async Task MergeMultipleVersions(string relKey, List<ModFileInfo> different,
                                                ModFileInfo? vanillaVersion, string targetPath)
        {
            string ext = Path.GetExtension(relKey).ToLowerInvariant();

            if (ext == ".png")
            {
                // Select best sprite
                var best = SelectBestSprite(different.OrderBy(d => d.ModNumber).ToList(), vanillaVersion);
                File.Copy(best.FilePath, targetPath, true);
            }
            else if (ext == ".ogg" || ext == ".wav" || ext == ".mp3")
            {
                // Last mod wins for audio
                var last = different.OrderBy(d => d.ModNumber).Last();
                File.Copy(last.FilePath, targetPath, true);
            }
            else
            {
                // Try git merge for text files
                bool ok = false;
                if (vanillaVersion != null)
                {
                    ok = _gitService.PerformGitMerge(vanillaVersion.FilePath, different, targetPath);
                }

                if (!ok)
                {
                    // Fallback: last mod wins
                    File.Copy(different.OrderBy(d => d.ModNumber).Last().FilePath, targetPath, true);
                }
            }

            await Task.CompletedTask; // Keep async signature
        }

        private ModFileInfo SelectBestSprite(List<ModFileInfo> sprites, ModFileInfo? vanillaVersion)
        {
            Console.WriteLine($"    Selecting best sprite from {sprites.Count} version(s)");

            // Prioritize valid PNGs from later mods
            for (int i = sprites.Count - 1; i >= 0; i--)
            {
                if (_pngUtils.IsValidPNG(sprites[i].FilePath))
                {
                    var info = new FileInfo(sprites[i].FilePath);
                    Console.WriteLine($"      Selected valid PNG from {sprites[i].ModName} ({info.Length} bytes)");
                    return sprites[i];
                }
                else
                {
                    Console.WriteLine($"      WARNING: Invalid PNG from {sprites[i].ModName}");
                }
            }

            // Fall back to vanilla if available
            if (vanillaVersion != null && _pngUtils.IsValidPNG(vanillaVersion.FilePath))
            {
                Console.WriteLine($"      All mod sprites invalid, using vanilla");
                return vanillaVersion;
            }

            // Last resort
            Console.WriteLine($"      WARNING: No valid sprites found, using {sprites[0].ModName} anyway");
            return sprites[0];
        }

        private bool FilesEqual(string file1, string file2)
        {
            if (!File.Exists(file1) || !File.Exists(file2))
                return false;

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            if (info1.Length != info2.Length)
                return false;

            using var fs1 = File.OpenRead(file1);
            using var fs2 = File.OpenRead(file2);

            byte[] buffer1 = new byte[4096];
            byte[] buffer2 = new byte[4096];

            while (true)
            {
                int bytes1 = fs1.Read(buffer1, 0, buffer1.Length);
                int bytes2 = fs2.Read(buffer2, 0, buffer2.Length);

                if (bytes1 != bytes2)
                    return false;

                if (bytes1 == 0)
                    return true;

                if (!buffer1.Take(bytes1).SequenceEqual(buffer2.Take(bytes2)))
                    return false;
            }
        }

        private void ValidateSprites(int chapter, GM3PConfig config)
        {
            Console.WriteLine("\nValidating sprites after merge...");

            string spritesPath = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "Sprites");

            if (!Directory.Exists(spritesPath))
            {
                Console.WriteLine("  No sprites directory found in merged output");
                return;
            }

            int total = 0;
            var invalidSprites = new List<string>();

            foreach (var pngFile in Directory.EnumerateFiles(spritesPath, "*.png", SearchOption.AllDirectories))
            {
                total++;
                if (!_pngUtils.IsValidPNG(pngFile))
                    invalidSprites.Add(Path.GetFileName(pngFile));
            }

            if (total == 0)
            {
                Console.WriteLine("  WARNING: No sprites found in merged output!");
            }
            else if (invalidSprites.Count > 0)
            {
                Console.WriteLine($"  WARNING: {invalidSprites.Count} invalid sprites detected after merge:");
                foreach (var sprite in invalidSprites.Take(10))
                    Console.WriteLine($"    - {sprite}");
                if (invalidSprites.Count > 10)
                    Console.WriteLine($"    ... and {invalidSprites.Count - 10} more");
            }
            else
            {
                Console.WriteLine($"  ✓ All {total} sprites validated successfully");
            }
        }


        private void SaveChangeStamp(int chapter, int changedThisChapter, bool anyCodeChanged,
                                    bool anySpriteChanged, bool assetOrderChanged, GM3PConfig config)
        {
            var stampDir = _directoryManager.GetCachePath(config, "running");
            Directory.CreateDirectory(stampDir);

            File.WriteAllText(
                Path.Combine(stampDir, $"chapter_{chapter}_changes.txt"),
                $"{changedThisChapter}|{(anyCodeChanged?1:0)}|{(anySpriteChanged?1:0)}|{(assetOrderChanged?1:0)}");
        }

        public async Task HandleNewObjects(GM3PConfig config)
        {
            for (int chapter = 0; chapter < config.ChapterAmount; chapter++)
            {
                Console.WriteLine($"Checking for new objects in chapter {chapter}...");

                string vanillaCodePath = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "0", "Objects", "CodeEntries");

                for (int modNumber = 2; modNumber < (config.ModAmount + 2); modNumber++)
                {
                    string modCodePath = _directoryManager.GetXDeltaCombinerPath(
                        config, chapter.ToString(), modNumber.ToString(), "Objects", "CodeEntries");

                    var newObjects = _assetHelper.FindNewObjects(vanillaCodePath, modCodePath);

                    if (newObjects.Count > 0)
                    {
                        Console.WriteLine($"Mod {modNumber - 1} adds {newObjects.Count} new objects: {string.Join(", ", newObjects)}");

                        string newObjectsFile = _directoryManager.GetXDeltaCombinerPath(
                            config, chapter.ToString(), modNumber.ToString(), "Objects", "NewObjects.txt");
                        File.WriteAllLines(newObjectsFile, newObjects);
                    }
                }
            }

            await Task.CompletedTask;
        }

        public async Task ImportWithNewObjects(GM3PConfig config)
        {
            for (int chapter = 0; chapter < config.ChapterAmount; chapter++)
            {
                if (config.ModToolPath == "skip")
                {
                    Console.WriteLine("Manual import mode...");
                    continue;
                }

                Console.WriteLine($"Processing Chapter {chapter}...");

                string workingDataWin = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), "1", "data.win");

                // Find mods with new objects
                var modsWithNewObjects = new Dictionary<int, List<string>>();
                for (int modNumber = 2; modNumber < (config.ModAmount + 2); modNumber++)
                {
                    string newObjectsFile = _directoryManager.GetXDeltaCombinerPath(
                        config, chapter.ToString(), modNumber.ToString(), "Objects", "NewObjects.txt");

                    if (!File.Exists(newObjectsFile)) continue;

                    var lines = File.ReadAllLines(newObjectsFile)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrWhiteSpace(l))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();

                    if (lines.Count > 0)
                    {
                        modsWithNewObjects[modNumber] = lines;
                        Console.WriteLine($"  Mod {modNumber - 1} adds {lines.Count} new objects");
                    }
                }

                // Choose base data.win
                if (modsWithNewObjects.Count == 0)
                {
                    Console.WriteLine("No new objects, using vanilla as base");
                    _fileLinker.LinkOrCopy(
                        _directoryManager.GetXDeltaCombinerPath(config, chapter.ToString(), "0", "data.win"),
                        workingDataWin);
                }
                else
                {
                    // Use the mod with most new objects as base
                    int baseModNumber = modsWithNewObjects
                        .OrderByDescending(kv => kv.Value.Count)
                        .ThenByDescending(kv => kv.Key)
                        .First().Key;

                    Console.WriteLine($"  Using Mod {baseModNumber - 1}'s data.win as base");

                    _fileLinker.LinkOrCopy(
                        _directoryManager.GetXDeltaCombinerPath(config, chapter.ToString(), baseModNumber.ToString(), "data.win"),
                        workingDataWin);
                }
                Console.WriteLine("Importing merged graphics...");
                Console.WriteLine("Importing merged code...");
                Console.WriteLine("Importing AssetOrder...");

                // Determine which scripts to run
                var scripts = new List<string>();

                bool hasSprites = HasSprites(chapter, config);
                bool hasCode = HasCode(chapter, config);
                bool hasAssetOrder = HasAssetOrder(chapter, config);

                if (hasSprites)
                {
                    scripts.Add("ImportGraphics.csx");
                    Console.WriteLine($"  Found modified sprites to import");
                }
                else
                {
                    Console.WriteLine($"  No modified sprites found");
                }

                if (hasCode)
                {
                    scripts.Add("ImportGML.csx");
                    Console.WriteLine($"  Found modified code to import");
                }
                else
                {
                    Console.WriteLine($"  No modified code found");
                }

                if (hasAssetOrder)
                {
                    scripts.Add("ImportAssetOrder.csx");
                    Console.WriteLine($"  Found modified AssetOrder to import");
                }
                else
                {
                    Console.WriteLine($"  No modified AssetOrder found");
                }

                if (scripts.Count > 0)
                {
                    await _modTool.RunImportScripts(workingDataWin, scripts.ToArray(), config);
                }
                else
                {
                    Console.WriteLine("  WARNING: No modifications detected to import!");
                }

                Console.WriteLine($"Import complete for chapter {chapter}");
            }
        }

        private bool HasSprites(int chapter, GM3PConfig config)
        {
            string spritesPath = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "Sprites");
            return Directory.Exists(spritesPath) &&
                   Directory.EnumerateFiles(spritesPath, "*.png", SearchOption.AllDirectories).Any();
        }

        private bool HasCode(int chapter, GM3PConfig config)
        {
            string codePath = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "CodeEntries");
            return Directory.Exists(codePath) &&
                   Directory.EnumerateFiles(codePath, "*.gml", SearchOption.AllDirectories).Any();
        }

        private bool HasAssetOrder(int chapter, GM3PConfig config)
        {
            string aoPath = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "AssetOrder.txt");
            return File.Exists(aoPath);
        }

        public List<string> GetModifiedAssets()
        {
            return _modifiedAssets;
        }
    }
}