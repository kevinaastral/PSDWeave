using System.IO;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    public sealed class SmartexportConsole : EditorWindow
    {
        private Vector2 scroll;
        private string reportPath;
        private string reportText;

        [MenuItem("Tools/Smartexport/Issues Console", false)]
        private static void Open()
        {
            GetWindow<SmartexportConsole>("Smartexport");
        }

        private void OnEnable()
        {
            LoadLatestReport();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Smartexport Issues", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("加载最新", GUILayout.Width(90)))
                    LoadLatestReport();
                if (GUILayout.Button("加载选中", GUILayout.Width(90)))
                    LoadSelectedReport();
                if (GUILayout.Button("复制", GUILayout.Width(90)))
                    EditorGUIUtility.systemCopyBuffer = reportText ?? string.Empty;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", reportPath ?? "(none)");

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(reportText) ? "暂无问题报告。" : reportText,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void LoadSelectedReport()
        {
            var asset = Selection.activeObject;
            var path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (string.IsNullOrEmpty(path))
                return;

            if (path.EndsWith(".prefab"))
            {
                var directory = Path.GetDirectoryName(path)?.Replace("\\", "/");
                var baseName = Path.GetFileNameWithoutExtension(path);
                if (baseName.EndsWith("_psd_layers_parsed"))
                    baseName = baseName.Substring(0, baseName.Length - "_psd_layers_parsed".Length);
                path = $"{directory}/{baseName}.ui_issues.json";
            }

            LoadReport(path);
        }

        private void LoadLatestReport()
        {
            var guids = AssetDatabase.FindAssets("ui_issues");
            string latestPath = null;
            var latestTicks = long.MinValue;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".ui_issues.json"))
                    continue;

                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    continue;

                var ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
                if (ticks > latestTicks)
                {
                    latestTicks = ticks;
                    latestPath = path;
                }
            }

            LoadReport(latestPath);
        }

        private void LoadReport(string path)
        {
            reportPath = path;
            reportText = null;

            if (string.IsNullOrEmpty(path))
                return;

            var fullPath = Path.GetFullPath(path);
            reportText = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }
    }
}
