using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace GM3P.Cache
{
    public interface IHashCache
    {
        string GetSha1Base64(string path);
        void Clear();
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

            using var fileStream = File.OpenRead(fullPath);
            using var sha = SHA1.Create();

            entry.Base64 = Convert.ToBase64String(sha.ComputeHash(fileStream));
            entry.Length = fileInfo.Length;
            entry.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

            return entry.Base64;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}