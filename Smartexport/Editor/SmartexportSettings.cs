using System.IO;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    public sealed class SmartexportSettings : ScriptableObject
    {
        private const string DefaultAssetPath = "Assets/Settings/Smartexport.asset";

        public bool EnableAutoGenerate = true;
        public float ConfidenceThreshold = 0.6f;

        public static SmartexportSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SmartexportSettings>(DefaultAssetPath);
            if (settings != null)
                return settings;

            settings = CreateInstance<SmartexportSettings>();
            var directory = Path.GetDirectoryName(DefaultAssetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
