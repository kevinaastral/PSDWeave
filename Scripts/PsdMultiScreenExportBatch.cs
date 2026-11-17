#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    /// <summary>
    /// One explicitly named screen export from a Smart Export collection PSD.
    /// Prelude roots are generated first, so shared backgrounds/masks remain
    /// behind the screen root in the resulting UGUI sibling order.
    /// </summary>
    public sealed class PsdMultiScreenExportRequest
    {
        public string SourceRootName;
        public string OutputName;
        public string[] PreludeManifestPaths;

        public PsdMultiScreenExportRequest(
            string sourceRootName,
            string outputName,
            params string[] preludeManifestPaths)
        {
            SourceRootName = sourceRootName;
            OutputName = outputName;
            PreludeManifestPaths = preludeManifestPaths ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Headless counterpart of the collection export window. It deliberately
    /// accepts explicit source/output mappings instead of guessing semantic
    /// English names or assigning shared layers by PSD stack order.
    /// </summary>
    public static class PsdMultiScreenExportBatch
    {
        public static string Run(
            string psdPath,
            string outputDir,
            IReadOnlyList<PsdMultiScreenExportRequest> requests,
            bool replaceExisting = true)
        {
            psdPath = NormalizeAssetPath(psdPath);
            outputDir = NormalizeAssetPath(outputDir)?.TrimEnd('/');
            ValidateAssetPath(psdPath, nameof(psdPath));
            ValidateAssetPath(outputDir, nameof(outputDir));
            if (!File.Exists(psdPath))
                throw new FileNotFoundException($"PSD not found: {psdPath}", psdPath);
            if (requests == null || requests.Count == 0)
                throw new ArgumentException("At least one screen export request is required.", nameof(requests));

            string manifestPath = NormalizeAssetPath(Path.Combine(
                Path.GetDirectoryName(psdPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(psdPath) + ".ui_manifest.json"));
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Smart Export manifest not found: {manifestPath}", manifestPath);
            var manifest = JsonUtility.FromJson<SmartExportManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || !string.Equals(manifest.schema, "psd2ugui.manifest/2.0", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported or missing Smart Export manifest schema: {manifestPath}");

            var normalizedOutputs = requests
                .Select((request, index) => PsdMultiScreenDetector.NormalizeOutputName(
                    request?.OutputName,
                    $"Screen{index + 1}"))
                .ToArray();
            var duplicateOutputs = normalizedOutputs
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateOutputs.Length > 0)
                throw new InvalidOperationException($"Duplicate output names: {string.Join(", ", duplicateOutputs)}");

            Directory.CreateDirectory(outputDir);
            AssetDatabase.ImportAsset(psdPath, ImportAssetOptions.ForceSynchronousImport);
            if (!Psd2UIFormConverter.ParsePsd2LayerPrefab(psdPath, null, false))
                throw new InvalidOperationException($"Parse PSD failed: {psdPath}");

            string parsedPrefabPath = NormalizeAssetPath(Psd2UIFormConverter.GetPsdLayerPrefabPath(psdPath));
            if (!File.Exists(parsedPrefabPath))
                throw new FileNotFoundException($"Parsed prefab not found: {parsedPrefabPath}", parsedPrefabPath);
            ProcessSmartExportManifestNow(parsedPrefabPath, manifestPath);

            GameObject parsedRoot = null;
            Psd2UIFormConverter converter = null;
            var generatedPaths = new List<string>();
            try
            {
                parsedRoot = PrefabUtility.LoadPrefabContents(parsedPrefabPath);
                converter = parsedRoot.GetComponent<Psd2UIFormConverter>();
                if (converter == null)
                    throw new InvalidOperationException($"Parsed prefab has no {nameof(Psd2UIFormConverter)}: {parsedPrefabPath}");

                var candidates = PsdMultiScreenDetector.Detect(converter);
                if (candidates.Count < 2)
                    throw new InvalidOperationException("Collection detector found fewer than two sibling screen roots.");

                var topLevelNodes = converter.transform
                    .Cast<Transform>()
                    .Select(child => child.GetComponent<PsdLayerNode>())
                    .Where(node => node != null)
                    .ToArray();

                for (int i = 0; i < requests.Count; i++)
                {
                    var request = requests[i] ?? throw new ArgumentException($"Request {i} is null.", nameof(requests));
                    var screenMatches = candidates
                        .Where(candidate => MatchesSourceName(candidate.Node, request.SourceRootName))
                        .ToArray();
                    if (screenMatches.Length != 1)
                    {
                        throw new InvalidOperationException(
                            $"Screen root '{request.SourceRootName}' matched {screenMatches.Length} detector candidates.");
                    }

                    string outputName = normalizedOutputs[i];
                    string outputPath = NormalizeAssetPath(Path.Combine(outputDir, outputName + ".prefab"));
                    if (replaceExisting && AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) != null)
                    {
                        if (!AssetDatabase.DeleteAsset(outputPath))
                            throw new InvalidOperationException($"Failed to replace existing prefab: {outputPath}");
                    }

                    var preludePaths = request.PreludeManifestPaths ?? Array.Empty<string>();
                    for (int preludeIndex = 0; preludeIndex < preludePaths.Length; preludeIndex++)
                    {
                        string preludePath = preludePaths[preludeIndex];
                        var manifestMatches = (manifest.layers ?? Array.Empty<SmartExportManifestLayer>())
                            .Where(layer => layer != null
                                && string.Equals(layer.path, preludePath, StringComparison.Ordinal))
                            .ToArray();
                        if (manifestMatches.Length != 1 || manifestMatches[0].flatIndex < 0)
                        {
                            throw new InvalidOperationException(
                                $"Prelude manifest path '{preludePath}' matched {manifestMatches.Length} valid layers.");
                        }
                        var manifestLayer = manifestMatches[0];
                        var smartNameMatches = topLevelNodes
                            .Where(node => MatchesSourceName(node, manifestLayer.name))
                            .ToArray();
                        PsdLayerNode preludeNode = null;
                        if (smartNameMatches.Length == 1)
                        {
                            preludeNode = smartNameMatches[0];
                        }
                        else
                        {
                            // Smart Export and the Unity PSD reader count flat
                            // indexes differently around section dividers. Keep
                            // flatIndex as a guarded fallback, never as the
                            // primary identity for a Smart Export named layer.
                            var flatIndexMatches = topLevelNodes
                                .Where(node => node.BindPsdLayerIndex == manifestLayer.flatIndex)
                                .ToArray();
                            if (flatIndexMatches.Length == 1)
                            {
                                preludeNode = flatIndexMatches[0];
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    $"Prelude manifest path '{preludePath}' (smartName={manifestLayer.name}, " +
                                    $"flatIndex={manifestLayer.flatIndex}) matched smartName={smartNameMatches.Length}, " +
                                    $"flatIndex={flatIndexMatches.Length} top-level parsed nodes.");
                            }
                        }
                        if (!converter.GenerateUIFormToDirectory(preludeNode, outputName, outputDir))
                            throw new InvalidOperationException($"Failed to export prelude '{preludePath}' for {outputName}.");
                    }

                    if (!converter.GenerateUIFormToDirectory(screenMatches[0].Node, outputName, outputDir))
                        throw new InvalidOperationException($"Failed to export screen '{request.SourceRootName}' as {outputName}.");

                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) == null)
                        throw new FileNotFoundException($"Generated prefab not found: {outputPath}", outputPath);
                    generatedPaths.Add(outputPath);
                }

                return $"parsed={parsedPrefabPath};candidates={candidates.Count};generated={generatedPaths.Count};" +
                       $"outputs={string.Join("|", generatedPaths)}";
            }
            finally
            {
                converter?.ReleasePsdFileHandle();
                if (parsedRoot != null)
                    PrefabUtility.UnloadPrefabContents(parsedRoot);
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static bool MatchesSourceName(PsdLayerNode node, string expectedName)
        {
            if (node == null || string.IsNullOrWhiteSpace(expectedName)) return false;
            return string.Equals(node.SourceLayerName, expectedName.Trim(), StringComparison.Ordinal)
                || string.Equals(node.name, expectedName.Trim(), StringComparison.Ordinal);
        }

        private static void ProcessSmartExportManifestNow(string prefabPath, string manifestPath)
        {
            const string processorTypeName =
                "UGF.EditorTools.Psd2UGUI.Smartexport.Editor.PsdManifestPostProcessor";
            var processorType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(processorTypeName, false))
                .FirstOrDefault(type => type != null);
            if (processorType == null)
                throw new InvalidOperationException($"Smart Export processor type not found: {processorTypeName}");

            var processMethod = processorType.GetMethod(
                "ProcessPrefabNow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            if (processMethod == null)
                throw new MissingMethodException(processorType.FullName, "ProcessPrefabNow(string, string)");

            try
            {
                processMethod.Invoke(null, new object[] { prefabPath, manifestPath });
            }
            catch (System.Reflection.TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void ValidateAssetPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path)
                || (!string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"{parameterName} must be a project-relative Assets path.", parameterName);
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace('\\', '/');
        }

        [Serializable]
        private sealed class SmartExportManifest
        {
            public string schema;
            public SmartExportManifestLayer[] layers;
        }

        [Serializable]
        private sealed class SmartExportManifestLayer
        {
            public int flatIndex = -1;
            public string path;
            public string name;
        }
    }
}
#endif
