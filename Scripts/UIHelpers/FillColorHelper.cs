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
    public class FillColorHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode fillColor;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(fillColor);
        }

        public override void ParseAndAttachUIElements()
        {
            if (LayerNode.UIType != GUIType.FillColor && LayerNode.LayerType != PsdLayerType.FillLayer)
            {
                LayerNode.SetUIType(UGUIParser.Instance.DefaultImage);
                return;
            }
            fillColor = LayerNode;
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            if (fillColor != null && fillColor.ShouldExportFillLayerAsImage())
            {
                var rawCom = uiRoot.GetComponentInChildren<UnityEngine.UI.RawImage>();
                var targetGo = rawCom != null ? rawCom.gameObject : uiRoot;
                if (rawCom != null)
                {
                    DestroyImmediate(rawCom);
                }

                var imageCom = targetGo.GetComponent<UnityEngine.UI.Image>();
                if (imageCom == null)
                {
                    imageCom = targetGo.AddComponent<UnityEngine.UI.Image>();
                }

                UGUIParser.SetRectTransform(fillColor, imageCom);
                imageCom.raycastTarget = false;
                imageCom.color = Color.white;
                imageCom.sprite = UGUIParser.LayerNode2Sprite(fillColor, imageCom.type == UnityEngine.UI.Image.Type.Sliced);
                if (imageCom.sprite != null && imageCom.sprite.border != Vector4.zero)
                {
                    imageCom.type = UnityEngine.UI.Image.Type.Sliced;
                }
                return;
            }

            var imgCom = uiRoot.GetComponentInChildren<UnityEngine.UI.RawImage>();
            if (imgCom == null)
            {
                imgCom = uiRoot.AddComponent<UnityEngine.UI.RawImage>();
            }
            UGUIParser.SetRectTransform(fillColor, imgCom);
            imgCom.raycastTarget = false;
            imgCom.color = UGUIParser.LayerNode2Color(fillColor, imgCom.color);
        }
    }
}
#endif
