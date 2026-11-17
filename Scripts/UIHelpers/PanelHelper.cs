/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public sealed class PanelHelper : UIHelperBase
    {
        [SerializeField] private PsdLayerNode background;

        public override PsdLayerNode[] GetDependencies()
        {
            RefreshBackgroundReference();
            return CalculateDependencies(background);
        }

        public override void ParseAndAttachUIElements()
        {
            RefreshBackgroundReference();
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            if (background != null)
            {
                if (uiRoot.GetComponent<CanvasRenderer>() == null)
                {
                    uiRoot.AddComponent<CanvasRenderer>();
                }
                var image = uiRoot.GetComponent<Image>() ?? uiRoot.AddComponent<Image>();
                image.sprite = UGUIParser.LayerNode2Sprite(background, image.type == Image.Type.Sliced);
                UGUIParser.SetRectTransform(LayerNode, image);
            }
            else
            {
                var image = uiRoot.GetComponent<Image>();
                if (image != null) DestroyImmediate(image);
                var canvasRenderer = uiRoot.GetComponent<CanvasRenderer>();
                if (canvasRenderer != null) DestroyImmediate(canvasRenderer);
                UGUIParser.SetRectTransform(LayerNode, uiRoot.transform);
            }
        }

        private void RefreshBackgroundReference()
        {
            background = LayerNode != null && LayerNode.LayerType == PsdLayerType.LayerGroup
                ? LayerNode.FindSubLayerNode(GUIType.Background)
                : null;
        }
    }
}
#endif
