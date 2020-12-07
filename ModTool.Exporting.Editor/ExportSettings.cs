using System.Collections.Generic;
using UnityEngine;
using ModTool.Shared;
using ModTool.Shared.Editor;

namespace ModTool.Exporting.Editor {
    /// <summary>
    /// Stores the exporter's settings.
    /// </summary>
    public class ExportSettings : EditorScriptableSingleton<ExportSettings> {
        /// <summary>
        /// The Mod's unique identifier.
        /// </summary>
        public static string id {
            get => instance._id;
            set => instance._id = value;
        }
        
        /// <summary>
        /// The Mod's name.
        /// </summary>
        public new static string name {
            get => instance._name;
            set => instance._name = value;
        }

        /// <summary>
        /// The Mod's author.
        /// </summary>
        public static string author {
            get => instance._author;
            set => instance._author = value;
        }

        /// <summary>
        /// The Mod's description.
        /// </summary>
        public static string description {
            get => instance._description;
            set => instance._description = value;
        }

        /// <summary>
        /// The Mod's version.
        /// </summary>
        public static string version {
            get => instance._version;
            set => instance._version = value;
        }

        /// <summary>
        /// The selected platforms for which this mod will be exported.
        /// </summary>
        public static ModPlatform platforms {
            get => instance._platforms;
            set => instance._platforms = value;
        }

        /// <summary>
        /// The selected content types that will be exported.
        /// </summary>
        public static ModContent content {
            get => instance._content;
            set => instance._content = value;
        }

        /// <summary>
        /// The directory to which the Mod will be exported.
        /// </summary>
        public static string outputDirectory {
            get => instance._outputDirectory;
            set => instance._outputDirectory = value;
        }
        
        /// <summary>
        /// The Mod dependencies
        /// </summary>
        public static List<Dependency> dependencies {
            get => instance._dependencies;
            set => instance._dependencies = value;
        }

        [SerializeField] private string _id;
        
        [SerializeField] private string _name;

        [SerializeField] private string _author;

        [SerializeField] private string _description;

        [SerializeField] private string _version;

        [SerializeField] private ModPlatform _platforms = (ModPlatform) (-1);

        [SerializeField] private ModContent _content = (ModContent) (-1);

        [SerializeField] private string _outputDirectory;
        
        [SerializeField] private List<Dependency> _dependencies;
    }
}