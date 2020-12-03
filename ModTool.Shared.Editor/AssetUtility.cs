using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ModTool.Shared.Editor {
    /// <summary>
    /// A set of utilities for handling assets.
    /// </summary>
    public static class AssetUtility {
        /// <summary>
        /// Finds and returns the directory where ModTool is located.
        /// </summary>
        /// <returns>The directory where ModTool is located.</returns>
        public static string GetModToolDirectory() {
            var Location = typeof(ModInfo).Assembly.Location;
            var ModToolDirectory = Path.GetDirectoryName(Location);

            if (!Directory.Exists(ModToolDirectory))
                ModToolDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");

            return GetRelativePath(ModToolDirectory);
        }

        /// <summary>
        /// Get the relative path for an absolute path.
        /// </summary>
        /// <param name="Path">The absolute path.</param>
        /// <returns>The relative path.</returns>
        public static string GetRelativePath(string Path) {
            var CurrentDirectory = Directory.GetCurrentDirectory();
            var PathUri = new Uri(Path);

            if (!CurrentDirectory.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                CurrentDirectory += System.IO.Path.DirectorySeparatorChar;

            var DirectoryUri = new Uri(CurrentDirectory);
            var RelativePath = Uri.UnescapeDataString(DirectoryUri.MakeRelativeUri(PathUri).ToString()
                .Replace('/', System.IO.Path.DirectorySeparatorChar));

            return RelativePath;
        }

        /// <summary>
        /// Get all asset paths for assets that match the filter.
        /// </summary>
        /// <param name="Filter">The filter string can contain search data for: names, asset labels and types (class names).</param>
        /// <returns>A list of asset paths</returns>
        public static IEnumerable<string> GetAssets(string Filter) {
            var AssetPaths = new List<string>();
            var AssetGuids = AssetDatabase.FindAssets(Filter);

            foreach (var Guid in AssetGuids) {
                var AssetPath = AssetDatabase.GUIDToAssetPath(Guid);

                if (AssetPath.Contains("ModTool"))
                    continue;

                if (AssetPath.Contains("/Editor/"))
                    continue;

                if (AssetPath.StartsWith("Packages"))
                    continue;

                //NOTE: AssetDatabase.FindAssets() can contain duplicates for some reason
                if (AssetPaths.Contains(AssetPath))
                    continue;

                AssetPaths.Add(AssetPath);
            }

            return AssetPaths;
        }

        /// <summary>
        /// Create an asset for a ScriptableObject in a ModTool Resources directory.
        /// </summary>
        /// <param name="ScriptableObject">A ScriptableObject instance.</param>
        public static void CreateAsset(ScriptableObject ScriptableObject) {
            var ResourcesParentDirectory = GetModToolDirectory();
            var ResourcesDirectory = "";

            ResourcesDirectory = Directory.GetDirectories(ResourcesParentDirectory,
                "Resources", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(ResourcesDirectory)) {
                ResourcesDirectory = System.IO.Path.Combine(ResourcesParentDirectory, "Resources");
                Directory.CreateDirectory(ResourcesDirectory);
            }

            var Path = System.IO.Path.Combine(ResourcesDirectory,
                ScriptableObject.GetType().Name + ".asset");

            AssetDatabase.CreateAsset(ScriptableObject, Path);
        }
    }
}