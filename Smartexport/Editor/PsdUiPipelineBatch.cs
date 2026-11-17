#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UGF.EditorTools.Psd2UGUI;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    public static class PsdUiPipelineBatch
    {
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var psdPaths = SplitArg(GetArg(args, "-psdPaths"));
            var outputNames = SplitArg(GetArg(args, "-outputNames"));
            var outputDir = GetArg(args, "-outputDir");
            var overwrite = !string.Equals(GetArg(args, "-overwrite"), "false", StringComparison.OrdinalIgnoreCase);

            Run(psdPaths, outputNames, outputDir, overwrite);
        }

        public static void Run(string[] psdPaths, string[] outputNames, string outputDir = null, bool overwrite = true)
        {
            psdPaths ??= Array.Empty<string>();
            outputNames ??= Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(outputDir))
                outputDir = Psd2UIFormSettings.Instance.UIFormOutputDir;
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ArgumentException("Set a UIForm output directory or pass -outputDir.", nameof(outputDir));

            if (psdPaths.Length == 0)
                throw new ArgumentException("Missing -psdPaths.");
            if (outputNames.Length != 0 && outputNames.Length != psdPaths.Length)
                throw new ArgumentException("-outputNames count must match -psdPaths count.");

            Directory.CreateDirectory(outputDir);

            for (var i = 0; i < psdPaths.Length; i++)
            {
                var psdPath = NormalizeAssetPath(psdPaths[i]);
                var outputName = outputNames.Length > 0
                    ? outputNames[i]
                    : Path.GetFileNameWithoutExtension(psdPath);

                GenerateOne(psdPath, outputDir, outputName, overwrite);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[PsdUiPipelineBatch] Completed.");
        }

        private static void GenerateOne(string psdPath, string outputDir, string outputName, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(psdPath))
                throw new ArgumentException("PSD path is empty.");
            if (!File.Exists(psdPath))
                throw new FileNotFoundException($"PSD not found: {psdPath}");

            Debug.Log($"[PsdUiPipelineBatch] Parse PSD: {psdPath}");
            AssetDatabase.ImportAsset(psdPath, ImportAssetOptions.ForceSynchronousImport);
            if (!Psd2UIFormConverter.ParsePsd2LayerPrefab(psdPath, openPrefabAfterSave: false))
                throw new InvalidOperationException($"Parse PSD failed: {psdPath}");

            var parsedPrefabPath = NormalizeAssetPath(Psd2UIFormConverter.GetPsdLayerPrefabPath(psdPath));
            if (!File.Exists(parsedPrefabPath))
                throw new FileNotFoundException($"Parsed prefab not found: {parsedPrefabPath}");

            var rawPrefabName = outputName.EndsWith("Panel", StringComparison.Ordinal)
                ? outputName
                : outputName + "Panel";
            var rawPrefabPath = NormalizeAssetPath(Path.Combine(outputDir, rawPrefabName + ".prefab"));
            if (!overwrite && File.Exists(rawPrefabPath))
            {
                Debug.Log($"[PsdUiPipelineBatch] Skip existing raw prefab: {rawPrefabPath}");
                return;
            }

            GameObject root = null;
            Psd2UIFormConverter converter = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(parsedPrefabPath);
                converter = root.GetComponent<Psd2UIFormConverter>();
                if (converter == null)
                    throw new InvalidOperationException($"Parsed prefab has no Psd2UIFormConverter: {parsedPrefabPath}");

                Debug.Log($"[PsdUiPipelineBatch] Generate UIForm: parsed={parsedPrefabPath}, output={rawPrefabName}");
                if (!converter.GenerateUIFormForBatch(rawPrefabName, outputDir))
                    throw new InvalidOperationException($"Generate UIForm failed: {rawPrefabPath}");
            }
            finally
            {
                converter?.ReleasePsdFileHandle();
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            if (!File.Exists(rawPrefabPath))
                throw new FileNotFoundException($"Raw UIForm prefab not generated: {rawPrefabPath}");
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return null;
        }

        private static string[] SplitArg(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}
#endif
