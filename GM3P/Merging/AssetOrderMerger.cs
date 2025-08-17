using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GM3P.Data;
using GM3P.FileSystem;

namespace GM3P.Merging
{
    public interface IAssetOrderMerger
    {
        void HandleAssetOrderFile(int chapter, Dictionary<string, List<ModFileInfo>> allFileVersions,
            Dictionary<string, string> vanillaFileDict, GM3PConfig config);
        List<string> MergeAssetOrderIntelligently(string? vanillaPath, List<ModFileInfo> modVersions);
        bool SanitizeAssetOrderForChapter(int chapter, GM3PConfig config);
        void ValidateAssetOrder(string assetOrderPath, int chapter);
    }

    public class AssetOrderMerger : IAssetOrderMerger
    {
        private readonly IDirectoryManager _directoryManager;

        public AssetOrderMerger(IDirectoryManager directoryManager)
        {
            _directoryManager = directoryManager;
        }

        public void HandleAssetOrderFile(int chapter, Dictionary<string, List<ModFileInfo>> allFileVersions,
            Dictionary<string, string> vanillaFileDict, GM3PConfig config)
        {
            Console.WriteLine("\n=== Processing AssetOrder.txt ===");

            string assetOrderFile = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "AssetOrder.txt");

            // Check if any mod has AssetOrder.txt
            if (!allFileVersions.ContainsKey("assetorder.txt"))
            {
                if (vanillaFileDict.ContainsKey("assetorder.txt"))
                {
                    Console.WriteLine("  No mods modify AssetOrder.txt, using vanilla");
                    File.Copy(vanillaFileDict["assetorder.txt"], assetOrderFile, true);
                }
                else
                {
                    Console.WriteLine("  WARNING: No AssetOrder.txt found in vanilla or mods!");
                }
                return;
            }

            var assetOrderVersions = allFileVersions["assetorder.txt"];
            var vanillaAssetOrder = assetOrderVersions.FirstOrDefault(v => v.ModNumber == 0);
            var modAssetOrders = assetOrderVersions.Where(v => v.ModNumber > 0).ToList();

            var differentModAssetOrders = new List<ModFileInfo>();

            if (vanillaAssetOrder != null)
            {
                var vanillaLines = File.ReadAllLines(vanillaAssetOrder.FilePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                foreach (var mod in modAssetOrders)
                {
                    try
                    {
                        var modLines = File.ReadAllLines(mod.FilePath)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

                        bool isDifferent = vanillaLines.Count != modLines.Count ||
                                         !vanillaLines.SequenceEqual(modLines);

                        if (isDifferent)
                        {
                            differentModAssetOrders.Add(mod);
                            Console.WriteLine($"  {mod.ModName} modifies AssetOrder.txt");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERROR reading {mod.ModName}'s AssetOrder.txt: {ex.Message}");
                        differentModAssetOrders.Add(mod);
                    }
                }
            }
            else
            {
                differentModAssetOrders = modAssetOrders;
                Console.WriteLine("  WARNING: No vanilla AssetOrder.txt found!");
            }

            // Process based on number of different versions
            if (differentModAssetOrders.Count == 0 && vanillaAssetOrder != null)
            {
                File.Copy(vanillaAssetOrder.FilePath, assetOrderFile, true);
                Console.WriteLine("  ✓ Using vanilla AssetOrder.txt (no mods modify it)");
            }
            else if (differentModAssetOrders.Count == 1)
            {
                File.Copy(differentModAssetOrders[0].FilePath, assetOrderFile, true);
                Console.WriteLine($"  ✓ Using {differentModAssetOrders[0].ModName}'s AssetOrder.txt");
            }
            else if (differentModAssetOrders.Count > 1)
            {
                Console.WriteLine($"Merging AssetOrder.txt from {differentModAssetOrders.Count} mods...");

                try
                {
                    var mergedAssets = MergeAssetOrderIntelligently(
                        vanillaAssetOrder?.FilePath,
                        differentModAssetOrders
                    );

                    File.WriteAllLines(assetOrderFile, mergedAssets);
                    Console.WriteLine($"  ✓ Merged AssetOrder.txt: {mergedAssets.Count} total assets");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR: Failed to merge AssetOrder.txt: {ex.Message}");
                    var lastMod = differentModAssetOrders.Last();
                    File.Copy(lastMod.FilePath, assetOrderFile, true);
                }
            }

            SanitizeAssetOrderForChapter(chapter, config);
            ValidateAssetOrder(assetOrderFile, chapter);
        }

        public List<string> MergeAssetOrderIntelligently(string? vanillaPath, List<ModFileInfo> modVersions)
        {
            var result = new List<string>();
            var addedAssets = new HashSet<string>();

            // Start with vanilla order if available
            List<string>? vanillaOrder = null;
            if (!string.IsNullOrEmpty(vanillaPath) && File.Exists(vanillaPath))
            {
                vanillaOrder = File.ReadAllLines(vanillaPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                foreach (var asset in vanillaOrder)
                {
                    result.Add(asset);
                    addedAssets.Add(asset);
                }

                Console.WriteLine($"    Base: {vanillaOrder.Count} vanilla assets");
            }

            // Process each mod's additions
            foreach (var mod in modVersions)
            {
                var modAssets = File.ReadAllLines(mod.FilePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
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
                    if (nextIndex == prevIndex + 1)
                    {
                        return nextIndex;
                    }
                }
                return prevIndex + 1;
            }
            else if (nextAsset != null)
            {
                return currentOrder.IndexOf(nextAsset);
            }

            return currentOrder.Count;
        }

        public bool SanitizeAssetOrderForChapter(int chapter, GM3PConfig config)
        {
            string baseAO = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "0", "Objects", "AssetOrder.txt");
            string mergedAO = _directoryManager.GetXDeltaCombinerPath(
                config, chapter.ToString(), "1", "Objects", "AssetOrder.txt");

            if (!File.Exists(mergedAO))
                return false;

            var baseLines = File.Exists(baseAO)
                ? File.ReadAllLines(baseAO).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList()
                : new List<string>();

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in baseLines) allowed.Add(l);

            // Add entries from all mod AssetOrder files
            for (int modFolder = 2; modFolder < (config.ModAmount + 2); modFolder++)
            {
                string modAO = _directoryManager.GetXDeltaCombinerPath(
                    config, chapter.ToString(), modFolder.ToString(), "Objects", "AssetOrder.txt");
                if (!File.Exists(modAO)) continue;

                foreach (var l in File.ReadAllLines(modAO))
                {
                    var s = l.Trim();
                    if (s.Length > 0) allowed.Add(s);
                }
            }

            var mergedOrig = File.ReadAllLines(mergedAO).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filtered = new List<string>();

            foreach (var s in mergedOrig)
            {
                if (!allowed.Contains(s)) continue;
                if (seen.Add(s)) filtered.Add(s);
            }

            // Append any missing entries
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
            Console.WriteLine($"  NOTE: kept {filtered.Count} of {mergedOrig.Count} AO lines; pruned {pruned}, appended {appended} (chapter {chapter}).");

            bool sameAsBase = baseLines.Count > 0 && filtered.SequenceEqual(baseLines, StringComparer.Ordinal);
            if (sameAsBase) return false;

            File.WriteAllLines(mergedAO, filtered);
            return true;
        }

        public void ValidateAssetOrder(string assetOrderPath, int chapter)
        {
            if (!File.Exists(assetOrderPath))
            {
                Console.WriteLine("  ERROR: AssetOrder.txt doesn't exist after merge!");
                return;
            }

            var lines = File.ReadAllLines(assetOrderPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                Console.WriteLine("  ERROR: AssetOrder.txt is empty!");
                return;
            }

            // Check for duplicates
            var duplicates = lines.GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                Console.WriteLine($"  WARNING: {duplicates.Count} duplicate entries in AssetOrder.txt:");
                foreach (var dup in duplicates.Take(5))
                {
                    Console.WriteLine($"    - {dup}");
                }
            }

            var spriteAssets = lines.Where(l => l.Contains("spr_") || l.Contains("Sprite")).ToList();
            Console.WriteLine($"  ✓ AssetOrder.txt validated: {lines.Count} total assets, {spriteAssets.Count} sprite-related");
        }
    }
}