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
    public class ImageHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode image;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(image);
        }

        public override void ParseAndAttachUIElements()
        {
            image = LayerNode;
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var imgCom = uiRoot.GetComponentInChildren<UnityEngine.UI.Image>();
            UGUIParser.SetRectTransform(image,imgCom);
            imgCom.raycastTarget = false;
            imgCom.sprite = UGUIParser.LayerNode2Sprite(image, imgCom.type == UnityEngine.UI.Image.Type.Sliced);
            // 九宫格裁剪的图片自动设为Sliced类型
            if (imgCom.sprite != null && imgCom.sprite.border != UnityEngine.Vector4.zero)
            {
                imgCom.type = UnityEngine.UI.Image.Type.Sliced;
            }
        }
    }
}
#endif