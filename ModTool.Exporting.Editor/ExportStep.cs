using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using ModTool.Shared;
using ModTool.Shared.Verification;
using ModTool.Shared.Editor;
using Mono.Cecil;
using System.Text.RegularExpressions;

namespace ModTool.Exporting.Editor {
    public abstract class ExportStep {
        protected static readonly string assetsDirectory = "Assets";
        protected static readonly string modToolDirectory = AssetUtility.GetModToolDirectory();
        protected static readonly string assemblyDirectory = Path.Combine("Library", "ScriptAssemblies");
        protected static readonly string tempAssemblyDirectory = Path.Combine("Temp", "ScriptAssemblies");
        protected static readonly string dllPath = Path.Combine(modToolDirectory, "ModTool.Interface.dll");

        protected static readonly string[] scriptAssemblies = {
            "Assembly-CSharp.dll",
            "Assembly-Csharp-firstpass.dll",
            "Assembly-UnityScript.dll",
            "Assembly-UnityScript-firstpass.dll"
            //"Assembly-Boo.dll",
            //"Assembly-Boo-firstpass.dll"
        };

        public bool waitForAssemblyReload { get; private set; }

        public abstract string message { get; }

        internal abstract void Execute(ExportData data);

        protected void ForceAssemblyReload() {
            waitForAssemblyReload = true;
            AssetDatabase.ImportAsset(dllPath, ImportAssetOptions.ForceUpdate);
        }
    }

    public class StartExport : ExportStep {
        public override string message => "Starting Export";

        internal override void Execute(ExportData data) {
            data.loadedScene = SceneManager.GetActiveScene().path;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new Exception("Cancelled by user");

            data.prefix = ExportSettings.id + "-";

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    public class Verify : ExportStep {
        public override string message => "Verifying Project";

        internal override void Execute(ExportData data) {
            CheckSerializationMode();
            VerifyProject();
            VerifySettings();
        }

        private void CheckSerializationMode() {
            if (EditorSettings.serializationMode != SerializationMode.ForceText) {
                LogUtility.LogInfo("Changed serialization mode from " + EditorSettings.serializationMode +
                                   " to Force Text");
                EditorSettings.serializationMode = SerializationMode.ForceText;
            }
        }

        private void VerifyProject() {
            if (!string.IsNullOrEmpty(ModToolSettings.unityVersion) &&
                Application.unityVersion != ModToolSettings.unityVersion)
                throw new Exception("Mods for " + ModToolSettings.productName + " can only be exported with Unity " +
                                    ModToolSettings.unityVersion);

            if (Application.isPlaying)
                throw new Exception("Unable to export mod in play mode");

            if (!VerifyAssemblies())
                throw new Exception("Incompatible scripts or assemblies found");
        }

        private void VerifySettings() {
            if (string.IsNullOrEmpty(ExportSettings.id))
                throw new Exception("Mod has no id");
            
            if (string.IsNullOrEmpty(ExportSettings.name))
                throw new Exception("Mod has no name");

            if (string.IsNullOrEmpty(ExportSettings.outputDirectory))
                throw new Exception("No output directory set");

            if (!Directory.Exists(ExportSettings.outputDirectory))
                throw new Exception("Output directory " + ExportSettings.outputDirectory + " does not exist");

            if (ExportSettings.platforms == 0)
                throw new Exception("No platforms selected");

            if (ExportSettings.content == 0)
                throw new Exception("No content selected");
        }

        private static bool VerifyAssemblies() {
            var assemblies = AssemblyUtility.GetAssemblies(assetsDirectory, AssemblyFilter.ModAssemblies);

            foreach (var scriptAssembly in scriptAssemblies) {
                var scriptAssemblyFile = Path.Combine(assemblyDirectory, scriptAssembly);

                if (File.Exists(scriptAssemblyFile))
                    assemblies.Add(scriptAssemblyFile);
            }

            var messages = new List<string>();

            AssemblyVerifier.VerifyAssemblies(assemblies, messages);

            foreach (var message in messages)
                LogUtility.LogWarning(message);

            if (messages.Count > 0)
                return false;

            return true;
        }

        [MenuItem("Tools/ModTool/Verify")]
        public static void VerifyScriptsMenuItem() {
            if (VerifyAssemblies())
                LogUtility.LogInfo("Scripts Verified!");
            else
                LogUtility.LogWarning("Scripts Not verified!");
        }
    }

    public class GetContent : ExportStep {
        public override string message => "Finding Content";

        internal override void Execute(ExportData data) {
            data.assemblies = GetAssemblies();
            data.assets = GetAssets("t:prefab t:scriptableobject");
            data.scenes = GetAssets("t:scene");
            data.scripts = GetAssets("t:monoscript");

            var content = ExportSettings.content;

            if (data.assets.Count == 0)
                content &= ~ModContent.Assets;
            if (data.scenes.Count == 0)
                content &= ~ModContent.Scenes;
            if (data.assemblies.Count == 0 && data.scripts.Count == 0)
                content &= ~ModContent.Code;

            data.content = content;
        }

        private List<Asset> GetAssets(string filter) {
            var assets = new List<Asset>();

            foreach (var path in AssetUtility.GetAssets(filter))
                assets.Add(new Asset(path));

            return assets;
        }

        private List<Asset> GetAssemblies() {
            var assemblies = new List<Asset>();

            foreach (var path in AssemblyUtility.GetAssemblies(assetsDirectory, AssemblyFilter.ModAssemblies)) {
                var assembly = new Asset(path);
                assembly.Move(modToolDirectory);
                assemblies.Add(assembly);
            }

            return assemblies;
        }
    }

    public class CreateBackup : ExportStep {
        public override string message => "Creating Backup";

        internal override void Execute(ExportData data) {
            AssetDatabase.SaveAssets();

            if (Directory.Exists(Asset.backupDirectory))
                Directory.Delete(Asset.backupDirectory, true);

            Directory.CreateDirectory(Asset.backupDirectory);

            if (Directory.Exists(tempAssemblyDirectory))
                Directory.Delete(tempAssemblyDirectory, true);

            Directory.CreateDirectory(tempAssemblyDirectory);

            foreach (var asset in data.assets)
                asset.Backup();

            foreach (var scene in data.scenes)
                scene.Backup();

            foreach (var script in data.scripts)
                script.Backup();

            foreach (var path in Directory.GetFiles(assemblyDirectory))
                File.Copy(path, Path.Combine(tempAssemblyDirectory, Path.GetFileName(path)));
        }
    }

    public class ImportScripts : ExportStep {
        public override string message => "Importing Script Assemblies";

        internal override void Execute(ExportData data) {
            if ((data.content & ModContent.Code) != ModContent.Code)
                return;

            foreach (var script in data.scripts)
                script.Delete();

            var prefix = data.prefix.Replace(" ", "");

            if (!string.IsNullOrEmpty(ExportSettings.version))
                prefix += ExportSettings.version.Replace(" ", "") + "-";

            var searchDirectories = GetSearchDirectories();

            foreach (var scriptAssembly in scriptAssemblies) {
                var scriptAssemblyPath = Path.Combine(tempAssemblyDirectory, scriptAssembly);

                if (!File.Exists(scriptAssemblyPath))
                    continue;

                var assembly = AssemblyDefinition.ReadAssembly(scriptAssemblyPath);
                var assemblyName = assembly.Name;

                var resolver = (DefaultAssemblyResolver) assembly.MainModule.AssemblyResolver;

                foreach (var searchDirectory in searchDirectories)
                    resolver.AddSearchDirectory(searchDirectory);

                assemblyName.Name = prefix + assemblyName.Name;

                foreach (var reference in assembly.MainModule.AssemblyReferences) {
                    if (reference.Name.Contains("firstpass"))
                        reference.Name = prefix + reference.Name;
                }

                scriptAssemblyPath = Path.Combine(modToolDirectory, assemblyName.Name + ".dll");

                assembly.Write(scriptAssemblyPath);

                data.scriptAssemblies.Add(new Asset(scriptAssemblyPath));
            }

            if (data.scriptAssemblies.Count > 0) {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
                ForceAssemblyReload();
            }
        }

        private static List<string> GetSearchDirectories() {
            var searchDirectories = new List<string>() {
                Path.GetDirectoryName(typeof(UnityEngine.Object).Assembly.Location),
                assetsDirectory
            };

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                if (a.GetName().Name == "netstandard")
                    searchDirectories.Add(Path.GetDirectoryName(a.Location));
            }

            return searchDirectories;
        }
    }

    public class UpdateAssets : ExportStep {
        public override string message => "Updating Assets";

        internal override void Execute(ExportData data) {
            var allAssets = data.assets.Concat(data.scenes);
            UpdateReferences(allAssets, data.scriptAssemblies);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);

            if ((data.content & ModContent.Assets) == ModContent.Assets) {
                foreach (var asset in data.assets)
                    asset.SetAssetBundle(ExportSettings.id, "assets");
            }

            if ((data.content & ModContent.Scenes) == ModContent.Scenes) {
                foreach (var scene in data.scenes) {
                    scene.name = data.prefix + scene.name;
                    scene.SetAssetBundle(ExportSettings.id, "scenes");
                }
            }
        }

        private static void UpdateReferences(IEnumerable<Asset> assets, IEnumerable<Asset> scriptAssemblies) {
            foreach (var scriptAssembly in scriptAssemblies)
                UpdateReferences(assets, scriptAssembly);
        }

        private static void UpdateReferences(IEnumerable<Asset> assets, Asset scriptAssembly) {
            var assemblyGuid = AssetDatabase.AssetPathToGUID(scriptAssembly.assetPath);
            var module = ModuleDefinition.ReadModule(scriptAssembly.assetPath);

            foreach (var asset in assets)
                UpdateReferences(asset, assemblyGuid, module.Types);
        }

        private static void UpdateReferences(Asset asset, string assemblyGuid, IEnumerable<TypeDefinition> types) {
            var lines = File.ReadAllLines(asset.assetPath);

            for (var i = 0; i < lines.Length; i++) {
                //Note: Line references script file - 11500000 is Unity's YAML class ID for MonoScript
                if (lines[i].Contains("11500000"))
                    lines[i] = UpdateReference(lines[i], assemblyGuid, types);
            }

            File.WriteAllLines(asset.assetPath, lines);
        }

        private static string UpdateReference(string line, string assemblyGuid, IEnumerable<TypeDefinition> types) {
            var guid = GetGuid(line);
            var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            var scriptName = Path.GetFileNameWithoutExtension(scriptPath);

            foreach (var type in types) {
                //script's type found, replace reference
                if (type.Name == scriptName) {
                    var fileID = GetTypeID(type.Namespace, type.Name).ToString();
                    line = line.Replace("11500000", fileID);
                    return line.Replace(guid, assemblyGuid);
                }
            }

            return line;
        }

        private static string GetGuid(string line) {
            var properties = Regex.Split(line, ", ");

            foreach (var property in properties) {
                if (property.Contains("guid: "))
                    return property.Remove(0, 6);
            }

            return "";
        }

        private static int GetTypeID(TypeDefinition type) {
            return GetTypeID(type.Namespace, type.Name);
        }

        private static int GetTypeID(string nameSpace, string typeName) {
            var toBeHashed = "s\0\0\0" + nameSpace + typeName;

            using (var hash = new MD4()) {
                var hashed = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(toBeHashed));

                var result = 0;

                for (var i = 3; i >= 0; --i) {
                    result <<= 8;
                    result |= hashed[i];
                }

                return result;
            }
        }
    }

    public class Export : ExportStep {
        public override string message => "Exporting Files";

        private string tempModDirectory;
        private string modDirectory;

        internal override void Execute(ExportData data) {
            tempModDirectory = Path.Combine("Temp", ExportSettings.id);
            modDirectory = Path.Combine(ExportSettings.outputDirectory, ExportSettings.id);

            if (Directory.Exists(tempModDirectory))
                Directory.Delete(tempModDirectory, true);

            Directory.CreateDirectory(tempModDirectory);

            foreach (var assembly in data.assemblies)
                assembly.Copy(tempModDirectory);

            foreach (var assembly in data.scriptAssemblies)
                assembly.Copy(tempModDirectory);

            var platforms = ExportSettings.platforms;

            BuildAssetBundles(platforms);

            var modInfo = new ModInfo(
                ExportSettings.id,
                ExportSettings.name,
                ExportSettings.author,
                ExportSettings.description,
                ExportSettings.version,
                Application.unityVersion,
                platforms,
                data.content);

            ModInfo.Save(Path.Combine(tempModDirectory, ExportSettings.id + ".info"), modInfo);

            CopyToOutput();

            if (data.scriptAssemblies.Count > 0)
                ForceAssemblyReload();
        }

        private void BuildAssetBundles(ModPlatform platforms) {
            var buildTargets = platforms.GetBuildTargets();

            foreach (var buildTarget in buildTargets) {
                var platformSubdirectory = Path.Combine(tempModDirectory, buildTarget.GetModPlatform().ToString());
                Directory.CreateDirectory(platformSubdirectory);
                BuildPipeline.BuildAssetBundles(platformSubdirectory, BuildAssetBundleOptions.None, buildTarget);
            }
        }

        private void CopyToOutput() {
            try {
                if (Directory.Exists(modDirectory))
                    Directory.Delete(modDirectory, true);

                CopyAll(tempModDirectory, modDirectory);

                LogUtility.LogInfo("Export complete");
            } catch (Exception e) {
                LogUtility.LogWarning("There was an issue while copying the mod to the output folder. " + e.Message);
            }
        }

        private static void CopyAll(string sourceDirectory, string targetDirectory) {
            Directory.CreateDirectory(targetDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory)) {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(targetDirectory, fileName), true);
            }

            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory)) {
                var targetSubDirectory = Path.Combine(targetDirectory, Path.GetFileName(subDirectory));
                CopyAll(subDirectory, targetSubDirectory);
            }
        }
    }

    public class RestoreProject : ExportStep {
        public override string message => "Restoring Project";

        internal override void Execute(ExportData data) {
            foreach (var scriptAssembly in data.scriptAssemblies)
                scriptAssembly.Delete();
            
            foreach (var asset in data.assets)
                asset.Restore();

            foreach (var scene in data.scenes)
                scene.Restore();

            foreach (var script in data.scripts)
                script.Restore();

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!string.IsNullOrEmpty(data.loadedScene))
                EditorSceneManager.OpenScene(data.loadedScene);
        }
    }
}