using System.Security.Cryptography;
using GM3P.Cache;

namespace GM3P.GameMaker
{
    public interface IPngUtils
    {
        bool IsValidPNG(string filePath);
        bool AreSpritesDifferent(string modPng, string vanillaPng);
        (int width, int height)? GetPngSize(string path);
        byte[]? HashPngIdat(string path);
    }

    public class PngUtils : IPngUtils
    {
        private static readonly byte[] PNG_SIGNATURE = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private readonly IHashCache _hashCache;

        public PngUtils(IHashCache hashCache)
        {
            _hashCache = hashCache;
        }

        public bool IsValidPNG(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 8)
                    return false;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[8];
                    fs.Read(header, 0, 8);
                    return header.SequenceEqual(PNG_SIGNATURE);
                }
            }
            catch
            {
                return false;
            }
        }

        public bool AreSpritesDifferent(string modPng, string vanillaPng)
        {
            try
            {
                // If mod PNG is invalid, skip importing it
                if (!IsValidPNG(modPng)) return false;

                // 1) Fast path: exact file hash match
                try
                {
                    var h1 = _hashCache.GetSha1Base64(modPng);
                    var h2 = _hashCache.GetSha1Base64(vanillaPng);
                    if (h1 == h2) return false;
                }
                catch { /* fall through */ }

                // 2) Geometry check
                var s1 = GetPngSize(modPng);
                var s2 = GetPngSize(vanillaPng);
                if (s1.HasValue && s2.HasValue &&
                    (s1.Value.width != s2.Value.width || s1.Value.height != s2.Value.height))
                    return true;

                // 3) Pixel compare when available
                try
                {
                    return !Codeuctivity.ImageSharpCompare.ImageSharpCompare.ImagesAreEqual(modPng, vanillaPng);
                }
                catch
                {
                    // 4) Fallback: compare IDAT data
                    var idat1 = HashPngIdat(modPng);
                    var idat2 = HashPngIdat(vanillaPng);
                    if (idat1 != null && idat2 != null)
                        return !idat1.SequenceEqual(idat2);

                    // 5) Last resort
                    try
                    {
                        using var a = File.OpenRead(modPng);
                        using var b = File.OpenRead(vanillaPng);
                        return !SHA1.Create().ComputeHash(a).SequenceEqual(SHA1.Create().ComputeHash(b));
                    }
                    catch { return true; }
                }
            }
            catch
            {
                return true; // Conservative: apply mods when uncertain
            }
        }

        public (int width, int height)? GetPngSize(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                if (fs.Length < 24) return null;

                Span<byte> sig = stackalloc byte[8];
                if (fs.Read(sig) != 8) return null;
                if (!sig.SequenceEqual(PNG_SIGNATURE)) return null;

                fs.Position = 16; // IHDR width/height
                Span<byte> wh = stackalloc byte[8];
                if (fs.Read(wh) != 8) return null;

                int w = (wh[0] << 24) | (wh[1] << 16) | (wh[2] << 8) | wh[3];
                int h = (wh[4] << 24) | (wh[5] << 16) | (wh[6] << 8) | wh[7];
                return (w, h);
            }
            catch { return null; }
        }

        public byte[]? HashPngIdat(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                // PNG signature
                byte[] sig = br.ReadBytes(8);
                if (sig.Length != 8 || sig[0] != 0x89) return null;

                using var sha1 = SHA1.Create();
                bool sawIdat = false;

                while (fs.Position < fs.Length)
                {
                    // chunk length (big endian)
                    var lenBytes = br.ReadBytes(4);
                    if (lenBytes.Length < 4) break;
                    int len = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];

                    // chunk type
                    var type = br.ReadBytes(4);
                    if (type.Length < 4) break;

                    // data
                    var data = br.ReadBytes(len);
                    if (data.Length < len) break;

                    // crc
                    br.ReadUInt32(); // skip CRC

                    // IDAT?
                    if (type[0] == (byte)'I' && type[1] == (byte)'D' && type[2] == (byte)'A' && type[3] == (byte)'T')
                    {
                        sawIdat = true;
                        sha1.TransformBlock(data, 0, data.Length, null, 0);
                    }

                    // IEND ends the stream
                    if (type[0] == (byte)'I' && type[1] == (byte)'E' && type[2] == (byte)'N' && type[3] == (byte)'D')
                        break;
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return sawIdat ? sha1.Hash : null;
            }
            catch { return null; }
        }
    }
}