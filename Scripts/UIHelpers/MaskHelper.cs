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
    public class MaskHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode mask;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(mask);
        }

        public override void ParseAndAttachUIElements()
        {
            mask = LayerNode;
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var imgCom = uiRoot.GetComponentInChildren<UnityEngine.UI.Image>();
            UGUIParser.SetRectTransform(mask, imgCom);
            imgCom.sprite = UGUIParser.LayerNode2Sprite(mask, imgCom.type == UnityEngine.UI.Image.Type.Sliced);
        }
    }
}
#endif