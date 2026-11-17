/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class RawImageHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode rawImage;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(rawImage);
        }

        public override void ParseAndAttachUIElements()
        {
            rawImage = LayerNode;
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var imgCom = uiRoot.GetComponentInChildren<UnityEngine.UI.RawImage>();
            UGUIParser.SetRectTransform(rawImage, imgCom);
            imgCom.raycastTarget = false;
            imgCom.texture = UGUIParser.LayerNode2Texture(rawImage);
        }
    }
}
#endif