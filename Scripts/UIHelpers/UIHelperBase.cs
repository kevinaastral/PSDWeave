/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public abstract class UIHelperBase : MonoBehaviour
    {
        public PsdLayerNode LayerNode => this.GetComponent<PsdLayerNode>();

        internal static bool SuppressOnEnable;

        private void OnEnable()
        {
            if (!SuppressOnEnable)
                ParseAndAttachUIElements();
        }

        /// <summary>
        /// 解析并关联UI元素,并且返回已经关联过的图层(已关联图层不再处理)
        /// </summary>
        public abstract void ParseAndAttachUIElements();

        /// <summary>
        /// 获取UI依赖的LayerNodes
        /// </summary>
        public abstract PsdLayerNode[] GetDependencies();

        /// <summary>
        /// 把UI实例进行UI元素初始化
        /// </summary>
        protected abstract void InitUIElements(GameObject uiRoot);

        /// <summary>
        /// 筛选出UI依赖的非空LayerNode
        /// </summary>
        protected PsdLayerNode[] CalculateDependencies(params PsdLayerNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0) return null;

            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node == null || node == LayerNode) ArrayUtility.RemoveAt(ref nodes, i);
            }
            return nodes;
        }

        internal GameObject CreateUI(GameObject uiInstance = null)
        {
            if (!this.LayerNode.IsMainUIType || LayerNode.UIType == GUIType.Null) return null;

            var rule = UGUIParser.Instance.GetRule(this.LayerNode.UIType);
            bool isProceduralContainer = LayerNode.UIType == GUIType.Panel
                || LayerNode.UIType == GUIType.ToggleGroup;
            if (!isProceduralContainer && (rule == null || rule.UIPrefab == null))
            {
                Debug.LogWarning($"创建UI类型{LayerNode.UIType}失败:Rule配置项不存在或UIPrefab为空");
                return null;
            }

            if (isProceduralContainer)
            {
                if (uiInstance != null && uiInstance.GetComponent<RectTransform>() == null)
                {
                    DestroyImmediate(uiInstance);
                    uiInstance = null;
                }

                if (uiInstance == null)
                {
                    uiInstance = new GameObject(LayerNode.name, typeof(RectTransform));
                }
            }
            else if (uiInstance != null && !IsCompatibleRoot(uiInstance, rule.UIPrefab))
            {
                DestroyImmediate(uiInstance);
                uiInstance = null;
            }

            if (!isProceduralContainer && uiInstance == null)
            {
                uiInstance = GameObject.Instantiate(rule.UIPrefab, Vector3.zero, Quaternion.identity);
            }

            if (LayerNode.IsMainUIType)
            {
                uiInstance.name = this.name;
            }

            InitUIElements(uiInstance);
            return uiInstance;
        }

        private static bool IsCompatibleRoot(GameObject existing, GameObject prefabRoot)
        {
            var expectedType = GetPrimaryRootComponentType(prefabRoot);
            var existingType = GetPrimaryRootComponentType(existing);
            if (expectedType == null || existingType == null)
            {
                return false;
            }

            return expectedType == existingType;
        }

        private static Type GetPrimaryRootComponentType(GameObject target)
        {
            if (target == null) return null;

            var components = target.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var type = component.GetType();
                if (type == typeof(Transform)
                    || type == typeof(RectTransform)
                    || type == typeof(CanvasRenderer)
                    || type == typeof(UIStringKey)
                    || type == typeof(PsdGeneratedKey))
                {
                    continue;
                }

                return type;
            }

            return null;
        }
    }
}
#endif
