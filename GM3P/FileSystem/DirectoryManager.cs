using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GM3P.Data;

namespace GM3P.FileSystem
{
    public interface IDirectoryManager
    {
        void CreateCombinerDirectories(GM3PConfig config);
        void CreateResultDirectories(GM3PConfig config, string modName);
        void ClearDirectory(string path, bool recursive = true);
        string GetCachePath(GM3PConfig config, params string[] segments);
        string GetXDeltaCombinerPath(GM3PConfig config, params string[] segments);
        List<string> FindDataWinFiles(string path);
    }

    public class DirectoryManager : IDirectoryManager
    {
        public void CreateCombinerDirectories(GM3PConfig config)
        {
            if (config.OutputPath == null)
                throw new InvalidOperationException("Output path not configured");

            Directory.CreateDirectory(Path.Combine(config.OutputPath, "Cache", "vanilla"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "Cache", "running"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "Cache", "export"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "Cache", "Logs"));
            Directory.CreateDirectory(Path.Combine(config.OutputPath, "xDeltaCombiner"));

            for (int chapter = 0; chapter < config.ChapterAmount; chapter++)
            {
                for (int modNumber = 0; modNumber < (config.ModAmount + 2); modNumber++)
                {
                    var path = Path.Combine(
                        config.OutputPath,
                        "xDeltaCombiner",
                        chapter.ToString(),
                        modNumber.ToString(),
                        "Objects");
                    Directory.CreateDirectory(path);
                }
            }
        }

        public void CreateResultDirectories(GM3PConfig config, string modName)
        {
            if (config.OutputPath == null)
                throw new InvalidOperationException("Output path not configured");

            var resultPath = Path.Combine(config.OutputPath, "result", modName);

            for (int chapter = 0; chapter < config.ChapterAmount; chapter++)
            {
                if (config.Combined)
                {
                    Directory.CreateDirectory(Path.Combine(resultPath, chapter.ToString()));
                }
                else
                {
                    for (int modNumber = 2; modNumber < (config.ModAmount + 2); modNumber++)
                    {
                        Directory.CreateDirectory(
                            Path.Combine(resultPath, chapter.ToString(), modNumber.ToString()));
                    }
                }
            }
        }

        public void ClearDirectory(string path, bool recursive = true)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        public string GetCachePath(GM3PConfig config, params string[] segments)
        {
            if (config.OutputPath == null)
                throw new InvalidOperationException("Output path not configured");

            var parts = new List<string> { config.OutputPath, "Cache" };
            parts.AddRange(segments);
            return Path.Combine(parts.ToArray());
        }

        public string GetXDeltaCombinerPath(GM3PConfig config, params string[] segments)
        {
            if (config.OutputPath == null)
                throw new InvalidOperationException("Output path not configured");

            var parts = new List<string> { config.OutputPath, "xDeltaCombiner" };
            parts.AddRange(segments);
            return Path.Combine(parts.ToArray());
        }

        public List<string> FindDataWinFiles(string path)
        {
            var winFiles = new List<string>();

            if (Path.GetExtension(path) == ".win")
            {
                if (File.Exists(path))
                    winFiles.Add(path);
            }
            else if (Directory.Exists(path))
            {
                // Check root
                string rootDataWin = Path.Combine(path, "data.win");
                if (File.Exists(rootDataWin))
                    winFiles.Add(rootDataWin);

                // Check subdirectories
                var directories = Directory.GetDirectories(path)
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);

                foreach (var dir in directories)
                {
                    string chapterDataWin = Path.Combine(dir, "data.win");
                    if (File.Exists(chapterDataWin))
                        winFiles.Add(chapterDataWin);
                }
            }

            return winFiles;
        }
    }
}