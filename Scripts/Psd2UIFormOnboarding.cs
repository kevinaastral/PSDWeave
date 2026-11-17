#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [InitializeOnLoad]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    internal static class Psd2UIFormOnboarding
    {
        private const string PrefKey = "UGF.Psd2UIForm.OnboardingShown";
        private static bool s_Initialized;

        static Psd2UIFormOnboarding()
        {
            EditorApplication.update += TryShow;
        }

        private static void TryShow()
        {
            if (s_Initialized) return;
            s_Initialized = true;
            EditorApplication.update -= TryShow;

            if (EditorPrefs.GetBool(PrefKey, false)) return;
            Psd2UIFormOnboardingWindow.ShowWindow();
        }

        internal static void MarkShown()
        {
            EditorPrefs.SetBool(PrefKey, true);
        }
    }

    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    internal class Psd2UIFormOnboardingWindow : EditorWindow
    {
        private bool dontShowAgain = true;

        public static void ShowWindow()
        {
            var window = GetWindow<Psd2UIFormOnboardingWindow>(true, "Psd2UIForm 新手引导", true);
            window.minSize = new Vector2(420, 260);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Psd2UIForm 新手引导", EditorStyles.boldLabel);
            GUILayout.Space(6);

            EditorGUILayout.LabelField("第一步：准备 PSD 文件", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("- 把你的 PSD 文件放入工程", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("或直接使用示例 PSD 文件测试", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("点击选中示例PSD文件：Psd2UguiForm_UGUI.psd", GUILayout.Height(24)))
            {
                LocateSamplePsd();
            }
            GUILayout.Space(6);

            EditorGUILayout.LabelField("第二步：解析 PSD", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("选中PSD文件, 鼠标右键单击 PSD 文件", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("选择菜单：Psd2UIForm Editor", EditorStyles.wordWrappedLabel);
            GUILayout.Space(6);

            EditorGUILayout.LabelField("第三步：生成 UIForm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("在编辑界面点击“生成UIForm”按钮，自动生成 UI 预制体", EditorStyles.wordWrappedLabel);

            GUILayout.FlexibleSpace();
            dontShowAgain = EditorGUILayout.ToggleLeft("下次不再提示", dontShowAgain);
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("视频教程", GUILayout.Height(28)))
            {
                Application.OpenURL("https://www.bilibili.com/video/BV1SVUkBWE2v");
            }
            if (GUILayout.Button("知道了", GUILayout.Height(28)))
            {
                if (dontShowAgain)
                {
                    Psd2UIFormOnboarding.MarkShown();
                }
                Close();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private static void LocateSamplePsd()
        {
            string[] guids = AssetDatabase.FindAssets("Psd2UguiForm_UGUI t:DefaultAsset t:Texture2D");
            if (guids != null && guids.Length > 0)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                    return;
                }
            }
            EditorUtility.DisplayDialog("未找到示例 PSD", "请确认示例文件已导入工程：Psd2UguiForm_UGUI.psd", "确定");
        }
    }
}
#endif
