using GM3P.Data;
using GM3P.FileSystem;

namespace GM3P.Cache
{
    public interface IExportCache
    {
        string GetDumpCacheDir(int chapter, int modNumber, GM3PConfig config);
        string GetDumpCacheDirByHash(int chapter, string hash, GM3PConfig config);
        string GetDumpStampPath(int chapter, int modNumber, GM3PConfig config);
        string GetDumpStampPathByHash(int chapter, string hash, GM3PConfig config);
        (string pre, string post) ReadStamp(string path);
        void WriteStamp(string path, string pre, string post);
        bool IsCacheValid(string stampPath, string expectedHash, GM3PConfig config);
        void PruneExportCacheIfNeeded(GM3PConfig config);
        void MirrorObjectsSelective(string srcObjects, string dstObjects, bool includeSprites);
    }

    public class ExportCache : IExportCache
    {
        private readonly IFileLinker _fileLinker;

        public ExportCache(IFileLinker fileLinker)
        {
            _fileLinker = fileLinker;
        }

        public string GetDumpCacheDir(int chapter, int modNumber, GM3PConfig config)
        {
            return Path.Combine(
                config.OutputPath ?? "",
                "Cache",
                "exports",
                chapter.ToString(),
                modNumber.ToString());
        }

        public string GetDumpCacheDirByHash(int chapter, string hash, GM3PConfig config)
        {
            var shard = string.IsNullOrEmpty(hash) ? "__" :
                hash.Substring(0, Math.Min(2, hash.Length));

            return Path.Combine(
                config.OutputPath ?? "",
                "Cache",
                "export",
                chapter.ToString(),
                "byhash",
                shard,
                hash);
        }

        public string GetDumpStampPath(int chapter, int modNumber, GM3PConfig config)
        {
            return Path.Combine(GetDumpCacheDir(chapter, modNumber, config), "dump.sha1");
        }

        public string GetDumpStampPathByHash(int chapter, string hash, GM3PConfig config)
        {
            return Path.Combine(GetDumpCacheDirByHash(chapter, hash, config), ".stamp");
        }

        public (string pre, string post) ReadStamp(string path)
        {
            try
            {
                var text = File.ReadAllText(path).Trim();
                string pre = "", post = "";

                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("pre="))
                        pre = trimmed.Substring(4);
                    else if (trimmed.StartsWith("post="))
                        post = trimmed.Substring(5);
                }

                // Back-compat: single-line format
                if (pre == "" && post == "" && !string.IsNullOrEmpty(text))
                    pre = text;

                return (pre, post);
            }
            catch
            {
                return ("", "");
            }
        }

        public void WriteStamp(string path, string pre, string post)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, $"pre={pre}\npost={post}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write stamp: {ex.Message}");
            }
        }

        public bool IsCacheValid(string stampPath, string expectedHash, GM3PConfig config)
        {
            if (!config.CacheEnabled || string.IsNullOrEmpty(expectedHash))
                return false;

            if (!File.Exists(stampPath))
                return false;

            var (pre, _) = ReadStamp(stampPath);
            return pre == expectedHash;
        }

        public void PruneExportCacheIfNeeded(GM3PConfig config)
        {
            if (config.ExportCacheCapMB <= 0)
                return;

            string root = Path.Combine(config.OutputPath ?? "", "Cache", "exports");
            if (!Directory.Exists(root))
                return;

            long capBytes = (long)config.ExportCacheCapMB * 1024 * 1024;
            long used = GetDirectorySize(root);

            if (used <= capBytes)
                return;

            // Collect entries with last access time and size
            var entries = new List<(string path, DateTime lastAccess, long size)>();

            foreach (var chapterDir in Directory.EnumerateDirectories(root))
            {
                foreach (var modDir in Directory.EnumerateDirectories(chapterDir))
                {
                    string stamp = Path.Combine(modDir, "dump.sha1");
                    DateTime lastAccess = File.Exists(stamp) ?
                        new FileInfo(stamp).LastWriteTimeUtc :
                        Directory.GetLastWriteTimeUtc(modDir);
                    long size = GetDirectorySize(modDir);
                    entries.Add((modDir, lastAccess, size));
                }
            }

            // Delete oldest first until under cap
            foreach (var entry in entries.OrderBy(e => e.lastAccess))
            {
                try
                {
                    Directory.Delete(entry.path, recursive: true);
                }
                catch
                {
                    // Continue even if deletion fails
                }

                used -= entry.size;
                if (used <= capBytes)
                    break;
            }
        }

        public void MirrorObjectsSelective(string srcObjects, string dstObjects, bool includeSprites)
        {
            if (!Directory.Exists(srcObjects))
                return;

            Directory.CreateDirectory(dstObjects);

            // Create directory structure
            foreach (var dir in Directory.EnumerateDirectories(srcObjects, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(srcObjects, dir);
                if (!includeSprites && relativePath.StartsWith("Sprites", StringComparison.OrdinalIgnoreCase))
                    continue;

                Directory.CreateDirectory(Path.Combine(dstObjects, relativePath));
            }

            // Copy/link files
            foreach (var file in Directory.EnumerateFiles(srcObjects, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(srcObjects, file);
                if (!includeSprites && relativePath.StartsWith("Sprites", StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = Path.Combine(dstObjects, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                _fileLinker.LinkOrCopy(file, target);
            }
        }

        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            long total = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // Skip files we can't access
                }
            }
            return total;
        }
    }
}