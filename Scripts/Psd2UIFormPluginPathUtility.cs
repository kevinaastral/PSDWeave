/*
 * Copyright (c) 2023-2026 Beijing Yiqu Technology Co., Ltd.
 *
 * All Rights Reserved.
 *
 * The software and associated documentation (including but not limited to source code,
 * object code, documents, images, audio, and video files) are the intellectual property
 * of Beijing Yiqu Technology Co., Ltd.
 *
 * For commercial use or licensing inquiries, please contact:
 * Email: efunstudio@gmail.com
 * Website: https://efunstudio.cn
 */

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class Psd2UIFormPluginPathUtility
    {
        internal const string PluginAssemblyName = "cn.efunstudio.psd2ugui";

        private const string ScriptsFolderToken = "/Scripts/";
        private static string cachedPluginRootAssetPath;
        private static string cachedProjectRoot;

        internal static string GetPluginRootAssetPath()
        {
            if (!string.IsNullOrWhiteSpace(cachedPluginRootAssetPath))
            {
                return cachedPluginRootAssetPath;
            }

            if (!string.Equals(typeof(UGUIParser).Assembly.GetName().Name, PluginAssemblyName, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var temp = ScriptableObject.CreateInstance<UGUIParser>();
            try
            {
                var script = MonoScript.FromScriptableObject(temp);
                string scriptPath = script != null ? AssetDatabase.GetAssetPath(script).Replace("\\", "/") : string.Empty;
                int scriptsIndex = scriptPath.IndexOf(ScriptsFolderToken, StringComparison.OrdinalIgnoreCase);
                if (scriptsIndex <= 0)
                {
                    return string.Empty;
                }

                cachedPluginRootAssetPath = scriptPath.Substring(0, scriptsIndex);
                return cachedPluginRootAssetPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        internal static string GetPluginAssetPath(string pluginRelativePath)
        {
            string root = GetPluginRootAssetPath();
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(pluginRelativePath))
            {
                return root;
            }

            return root.TrimEnd('/', '\\') + "/" + pluginRelativePath.Replace("\\", "/").TrimStart('/');
        }

        internal static string GetPluginAbsolutePath(string pluginRelativePath)
        {
            return AssetPathToAbsolutePath(GetPluginAssetPath(pluginRelativePath));
        }

        internal static string AssetPathToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (package != null)
                {
                    string relativePath = assetPath.Substring(package.assetPath.Length).TrimStart('/');
                    return Path.Combine(package.resolvedPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                }
            }

            string projectRoot = GetProjectRoot();
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return assetPath;
            }

            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static string GetProjectRoot()
        {
            if (!string.IsNullOrWhiteSpace(cachedProjectRoot))
            {
                return cachedProjectRoot;
            }

            cachedProjectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return cachedProjectRoot;
        }
    }
}
#endif
