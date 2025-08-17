using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GM3P.GameMaker
{
    public interface IAssetHelper
    {
        string? ExtractObjectName(string filename);
        HashSet<string> FindNewObjects(string vanillaCodePath, string modCodePath);
        bool IsEmptyGml(string filePath);
        string NormalizeKey(string relativePath);
    }

    public class AssetHelper : IAssetHelper
    {
        private readonly string[] _eventTypes =
        {
            "_Create_", "_Step_", "_Draw_", "_Alarm_",
            "_Destroy_", "_Collision_", "_Other_", "_PreCreate_"
        };

        public string? ExtractObjectName(string filename)
        {
            if (!filename.StartsWith("gml_Object_"))
                return null;

            int startIndex = "gml_Object_".Length;

            foreach (string eventType in _eventTypes)
            {
                int endIndex = filename.IndexOf(eventType);
                if (endIndex > startIndex)
                {
                    return filename.Substring(startIndex, endIndex - startIndex);
                }
            }

            return null;
        }

        public HashSet<string> FindNewObjects(string vanillaCodePath, string modCodePath)
        {
            var vanillaObjects = new HashSet<string>();
            var newObjects = new HashSet<string>();

            // Get vanilla objects
            if (Directory.Exists(vanillaCodePath))
            {
                foreach (string file in Directory.GetFiles(vanillaCodePath, "gml_Object_*"))
                {
                    string? objectName = ExtractObjectName(Path.GetFileName(file));
                    if (!string.IsNullOrEmpty(objectName))
                        vanillaObjects.Add(objectName);
                }
            }

            // Find new objects in mod
            if (Directory.Exists(modCodePath))
            {
                foreach (string file in Directory.GetFiles(modCodePath, "gml_Object_*"))
                {
                    string? objectName = ExtractObjectName(Path.GetFileName(file));
                    if (!string.IsNullOrEmpty(objectName) && !vanillaObjects.Contains(objectName))
                        newObjects.Add(objectName);
                }
            }

            return newObjects;
        }

        public bool IsEmptyGml(string filePath)
        {
            return filePath.EndsWith(".gml", StringComparison.OrdinalIgnoreCase) &&
                   new FileInfo(filePath).Length == 0;
        }

        public string NormalizeKey(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return string.Empty;

            relativePath = relativePath.Replace('\\', '/');
            if (relativePath.StartsWith("./"))
                relativePath = relativePath.Substring(2);

            return relativePath.ToLowerInvariant();
        }
    }
}