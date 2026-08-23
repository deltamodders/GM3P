using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GM3P.Data;
using GM3P.FileSystem;

namespace GM3P.Merging
{
    public interface IGlobalInitMerger
    {
        void HandleGlobalInitFile(int chapter, Dictionary<string, List<ModFileInfo>> allFileVersions,
            Dictionary<string, string> vanillaFileDict, GM3PConfig config);
        List<string> MergeGlobalInitIntelligently(string? vanillaPath, List<ModFileInfo> modVersions);
        bool SanitizeGlobalInitForChapter(int chapter, GM3PConfig config);
        void ValidateGlobalInit(string GlobalInitPath, int chapter);
    }

    public class GlobalInitMerger : IGlobalInitMerger
    {
        private readonly IDirectoryManager _directoryManager;

        public GlobalInitMerger(IDirectoryManager directoryManager)
        {
            _directoryManager = directoryManager;
        }

        // NEW: strict filter for real asset lines (keeps importer happy)
        private static bool IsRealAssetLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            // Section markers from exporters (should never be treated as asset names)
            if (s.StartsWith("@@")) return false;

            // Comments / separators
            if (s.StartsWith("#")) return false;

            // Defensive: reject obvious separators or malformed entries
            if (s.Contains(",") || s.Contains(":")) return false;

            return true;
        }

        public void HandleGlobalInitFile(int chapter, Dictionary<string, List<ModFileInfo>> allFileVersions,
            Dictionary<string, string> vanillaFileDict, GM3PConfig config)
        {
            Console.WriteLine("\n=== Processing GlobalInit.txt ===");

            string GlobalInitFile = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "GlobalInit.txt");

            // Check if any mod has GlobalInit.txt
            if (!allFileVersions.ContainsKey("GlobalInit.txt"))
            {
                if (vanillaFileDict.ContainsKey("GlobalInit.txt"))
                {
                    Console.WriteLine("  No mods modify GlobalInit.txt, using vanilla");
                    File.Copy(vanillaFileDict["GlobalInit.txt"], GlobalInitFile, true);
                }
                else
                {
                    Console.WriteLine("  WARNING: No GlobalInit.txt found at all; creating empty list");
                    File.WriteAllText(GlobalInitFile, string.Empty);
                }

                SanitizeGlobalInitForChapter(chapter, config);
                ValidateGlobalInit(GlobalInitFile, chapter);
                return;
            }

            var modGlobalInits = allFileVersions["GlobalInit.txt"];
            var differentModGlobalInits = new List<ModFileInfo>();

            // Use the latest version for each unique mod
            var groupedByMod = modGlobalInits.GroupBy(m => m.ModName, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, ModFileInfo> group in groupedByMod)
            {
                // Higher ModNumber wins; tie-break with file write time
                var latest = group
                    .OrderByDescending((ModFileInfo m) => m.ModNumber)
                    .ThenByDescending((ModFileInfo m) => File.GetLastWriteTimeUtc(m.FilePath))
                    .FirstOrDefault();

                if (latest != null)
                    differentModGlobalInits.Add(latest);
            }

            // Identify vanilla AO, if present
            ModFileInfo? vanillaGlobalInit = null;
            if (vanillaFileDict.ContainsKey("GlobalInit.txt"))
            {
                vanillaGlobalInit = new ModFileInfo
                {
                    FilePath = vanillaFileDict["GlobalInit.txt"],
                    ModName = "Vanilla",
                    ModNumber = 0
                };
            }
            else
            {
                differentModGlobalInits = modGlobalInits;
                Console.WriteLine("  WARNING: No vanilla GlobalInit.txt found!");
            }

            // Process based on number of different versions
            if (differentModGlobalInits.Count == 0 && vanillaGlobalInit != null)
            {
                File.Copy(vanillaGlobalInit.FilePath, GlobalInitFile, true);
                Console.WriteLine("  ✓ Using vanilla GlobalInit.txt (no mods modify it)");
            }
            else if (differentModGlobalInits.Count == 1)
            {
                // Single mod version – just use it
                var mod = differentModGlobalInits.First();
                File.Copy(mod.FilePath, GlobalInitFile, true);
                Console.WriteLine($"  ✓ Using GlobalInit.txt from {mod.ModName}");
            }
            else
            {
                try
                {
                    // Merge intelligently with vanilla context if available
                    string? vanillaPath = vanillaGlobalInit?.FilePath;
                    var mergedAssets = MergeGlobalInitIntelligently(vanillaPath, differentModGlobalInits);

                    File.WriteAllLines(GlobalInitFile, mergedAssets);
                    Console.WriteLine($"  ✓ Merged GlobalInit.txt: {mergedAssets.Count} total assets");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: Failed to merge GlobalInit.txt: {ex.Message}");
                    var lastMod = differentModGlobalInits.Last();
                    File.Copy(lastMod.FilePath, GlobalInitFile, true);
                }
            }

            SanitizeGlobalInitForChapter(chapter, config);
            ValidateGlobalInit(GlobalInitFile, chapter);
        }

        public List<string> MergeGlobalInitIntelligently(string? vanillaPath, List<ModFileInfo> modVersions)
        {
            var result = new List<string>();
            var addedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Start with vanilla order if available
            List<string>? vanillaOrder = null;
            if (!string.IsNullOrEmpty(vanillaPath) && File.Exists(vanillaPath))
            {
                vanillaOrder = File.ReadAllLines(vanillaPath)
                    .Select(l => l.Trim())
                    .Where(IsRealAssetLine)
                    .ToList();

                foreach (var asset in vanillaOrder)
                {
                    if (addedAssets.Add(asset))
                        result.Add(asset);
                }

                Console.WriteLine($"    Base: {vanillaOrder.Count} vanilla assets");
            }

            // Merge each mod's AO
            foreach (var mod in modVersions)
            {
                var modAssets = File.ReadAllLines(mod.FilePath)
                    .Select(l => l.Trim())
                    .Where(IsRealAssetLine)
                    .ToList();

                int newAssetsFromMod = 0;

                for (int i = 0; i < modAssets.Count; i++)
                {
                    string asset = modAssets[i];

                    if (!addedAssets.Contains(asset))
                    {
                        // Find best position for new asset
                        int insertPosition = FindBestInsertPosition(result, modAssets, i, asset);

                        if (insertPosition >= 0 && insertPosition <= result.Count)
                        {
                            result.Insert(insertPosition, asset);
                        }
                        else
                        {
                            result.Add(asset);
                        }

                        addedAssets.Add(asset);
                        newAssetsFromMod++;
                    }
                }

                if (newAssetsFromMod > 0)
                {
                    Console.WriteLine($"    {mod.ModName}: added {newAssetsFromMod} new assets");
                }
            }

            return result;
        }

        private int FindBestInsertPosition(List<string> currentOrder, List<string> modOrder,
                                          int modIndex, string newAsset)
        {
            string? previousAsset = null;
            string? nextAsset = null;

            // Find previous asset that's already in our list
            for (int i = modIndex - 1; i >= 0; i--)
            {
                if (currentOrder.Contains(modOrder[i]))
                {
                    previousAsset = modOrder[i];
                    break;
                }
            }

            // Find next asset that's already in our list
            for (int i = modIndex + 1; i < modOrder.Count; i++)
            {
                if (currentOrder.Contains(modOrder[i]))
                {
                    nextAsset = modOrder[i];
                    break;
                }
            }

            // Determine insert position based on context
            if (previousAsset != null)
            {
                int prevIndex = currentOrder.IndexOf(previousAsset);
                if (nextAsset != null)
                {
                    int nextIndex = currentOrder.IndexOf(nextAsset);
                    if (nextIndex > prevIndex)
                        return nextIndex; // place right before the next known asset
                    return prevIndex + 1;
                }
                else
                {
                    return prevIndex + 1;
                }
            }
            else if (nextAsset != null)
            {
                return currentOrder.IndexOf(nextAsset);
            }

            return currentOrder.Count;
        }

        public bool SanitizeGlobalInitForChapter(int chapter, GM3PConfig config)
        {
            string baseAO = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "0", "Objects", "GlobalInit.txt");
            string mergedAO = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "GlobalInit.txt");

            if (!File.Exists(mergedAO))
                return false;

            var baseLines = File.Exists(baseAO)
                ? File.ReadAllLines(baseAO).Select(l => l.Trim()).Where(IsRealAssetLine).ToList()
                : new List<string>();

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in baseLines) allowed.Add(l);

            // Add entries from all mod GlobalInit files
            for (int modFolder = 2; modFolder < (config.ModAmount + 2); modFolder++)
            {
                string modAO = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), modFolder.ToString(), "Objects", "GlobalInit.txt");
                if (!File.Exists(modAO)) continue;

                foreach (var l in File.ReadAllLines(modAO))
                {
                    var s = l.Trim();
                    if (IsRealAssetLine(s)) allowed.Add(s);
                }
            }

            // Filter merged AO to allowed, real asset lines, preserving order and removing dups
            var mergedOrig = File.ReadAllLines(mergedAO).Select(l => l.Trim()).Where(IsRealAssetLine).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filtered = new List<string>();

            foreach (var s in mergedOrig)
            {
                if (!allowed.Contains(s)) continue;
                if (seen.Add(s)) filtered.Add(s);
            }

            // Append any missing entries (real assets only)
            int appended = 0;
            foreach (var s in allowed)
            {
                if (!seen.Contains(s))
                {
                    filtered.Add(s);
                    appended++;
                }
            }

            int pruned = mergedOrig.Count - filtered.Count + appended;
            Console.WriteLine($"  NOTE: kept {filtered.Count} of {mergedOrig.Count} lines; pruned {pruned}, appended {appended} (chapter {chapter}).");

            bool sameAsBase = baseLines.Count > 0 && filtered.SequenceEqual(baseLines, StringComparer.Ordinal);
            if (sameAsBase) return false;

            File.WriteAllLines(mergedAO, filtered);
            return true;
        }

        public void ValidateGlobalInit(string GlobalInitPath, int chapter)
        {
            if (!File.Exists(GlobalInitPath))
            {
                Console.WriteLine("  ERROR: GlobalInit.txt doesn't exist after merge!");
                return;
            }

            var lines = File.ReadAllLines(GlobalInitPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine("  ERROR: GlobalInit.txt is empty!");
                return;
            }

            // Basic duplicate check
            var duplicates = lines.GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                Console.WriteLine($"  WARNING: {duplicates.Count} duplicate entries in GlobalInit.txt:");
                foreach (var dup in duplicates.Take(5))
                {
                    Console.WriteLine($"    - {dup}");
                }
            }

            var spriteAssets = lines.Where(l => l.Contains("spr_") || l.Contains("Sprite")).ToList();
            Console.WriteLine($"  ✓ GlobalInit.txt validated: {lines.Count} total assets, {spriteAssets.Count} sprite-related");
        }
    }
}