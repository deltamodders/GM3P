using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.IO.MemoryMappedFiles;
using System.Buffers;

namespace GM3P.Cache
{
    public interface IHashCache
    {
        string GetSha1Base64(string path);
        Task<string> GetSha1Base64Async(string path);
        void PrecomputeHashes(IEnumerable<string> paths);
        void Clear();
        void SaveCache(string path);
        void LoadCache(string path);
    }

    public class HashCache : IHashCache
    {
        private sealed class Entry
        {
            public long Length { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }
            public string Base64 { get; set; } = "";
        }

        private readonly ConcurrentDictionary<string, Entry> _cache = new();
        private readonly ConcurrentDictionary<string, Task<string>> _pendingHashes = new();
        private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
        private const int BufferSize = 81920; // 80KB buffer for optimal I/O

        public string GetSha1Base64(string path)
        {
            string fullPath = Path.GetFullPath(path);
            var fileInfo = new FileInfo(fullPath);

            var entry = _cache.GetOrAdd(fullPath, _ => new Entry());

            if (entry.Length == fileInfo.Length &&
                entry.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                entry.Base64.Length > 0)
            {
                return entry.Base64;
            }

            // For large files, use memory mapping
            if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
            {
                return ComputeHashMemoryMapped(fullPath, fileInfo, entry);
            }
            else
            {
                return ComputeHashStreaming(fullPath, fileInfo, entry);
            }
        }

        public async Task<string> GetSha1Base64Async(string path)
        {
            string fullPath = Path.GetFullPath(path);

            // Check if we're already computing this hash
            if (_pendingHashes.TryGetValue(fullPath, out var pendingTask))
            {
                return await pendingTask;
            }

            var fileInfo = new FileInfo(fullPath);
            var entry = _cache.GetOrAdd(fullPath, _ => new Entry());

            if (entry.Length == fileInfo.Length &&
                entry.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                entry.Base64.Length > 0)
            {
                return entry.Base64;
            }

            // Create and register the task
            var hashTask = Task.Run(() => GetSha1Base64(path));
            _pendingHashes[fullPath] = hashTask;

            try
            {
                return await hashTask;
            }
            finally
            {
                _pendingHashes.TryRemove(fullPath, out _);
            }
        }

        private string ComputeHashStreaming(string fullPath, FileInfo fileInfo, Entry entry)
        {
            var buffer = _bufferPool.Rent(BufferSize);
            try
            {
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, bufferSize: BufferSize,
                    options: FileOptions.SequentialScan);
                using var sha = SHA1.Create();

                int bytesRead;
                while ((bytesRead = fs.Read(buffer, 0, BufferSize)) > 0)
                {
                    sha.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                entry.Base64 = Convert.ToBase64String(sha.Hash!);
                entry.Length = fileInfo.Length;
                entry.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

                return entry.Base64;
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }

        private string ComputeHashMemoryMapped(string fullPath, FileInfo fileInfo, Entry entry)
        {
            try
            {
                using var mmf = MemoryMappedFile.CreateFromFile(fullPath,
                    FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                using var sha = SHA1.Create();

                long fileSize = fileInfo.Length;
                long position = 0;
                var buffer = _bufferPool.Rent(BufferSize);

                try
                {
                    while (position < fileSize)
                    {
                        int toRead = (int)Math.Min(BufferSize, fileSize - position);
                        accessor.ReadArray(position, buffer, 0, toRead);
                        sha.TransformBlock(buffer, 0, toRead, null, 0);
                        position += toRead;
                    }
                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                    entry.Base64 = Convert.ToBase64String(sha.Hash!);
                    entry.Length = fileInfo.Length;
                    entry.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

                    return entry.Base64;
                }
                finally
                {
                    _bufferPool.Return(buffer);
                }
            }
            catch
            {
                // Fallback to streaming if memory mapping fails
                return ComputeHashStreaming(fullPath, fileInfo, entry);
            }
        }

        public void PrecomputeHashes(IEnumerable<string> paths)
        {
            var pathList = paths.ToList();
            Console.WriteLine($"Precomputing hashes for {pathList.Count} files...");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            Parallel.ForEach(pathList, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            path =>
            {
                try
                {
                    GetSha1Base64(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to hash {path}: {ex.Message}");
                }
            });

            sw.Stop();
            Console.WriteLine($"Precomputed {pathList.Count} hashes in {sw.Elapsed.TotalSeconds:F2} seconds");
        }

        public void Clear()
        {
            _cache.Clear();
            _pendingHashes.Clear();
        }

        public void SaveCache(string path)
        {
            try
            {
                var cacheData = _cache.Select(kvp => new
                {
                    Path = kvp.Key,
                    kvp.Value.Length,
                    kvp.Value.LastWriteTimeUtc,
                    kvp.Value.Base64
                }).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(cacheData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, json);

                Console.WriteLine($"Saved hash cache with {cacheData.Count} entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save hash cache: {ex.Message}");
            }
        }

        public void LoadCache(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var json = File.ReadAllText(path);
                var cacheData = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(json);

                if (cacheData != null)
                {
                    foreach (var item in cacheData)
                    {
                        var entry = new Entry
                        {
                            Length = item.Length,
                            LastWriteTimeUtc = item.LastWriteTimeUtc,
                            Base64 = item.Base64
                        };
                        _cache[item.Path] = entry;
                    }

                    Console.WriteLine($"Loaded hash cache with {cacheData.Count} entries");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load hash cache: {ex.Message}");
            }
        }
    }
}