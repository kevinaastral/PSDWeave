/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class TMPToggleHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode background;
        [SerializeField] PsdLayerNode checkmark;
        [SerializeField] PsdLayerNode label;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, checkmark, label);
        }

        public override void ParseAndAttachUIElements()
        {
            background = LayerNode.FindSubLayerNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            checkmark = LayerNode.FindSubLayerNode(GUIType.Toggle_Checkmark);
            label = LayerNode.FindSubLayerNode(GUIType.Toggle_Label, GUIType.TMPText, GUIType.Text);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var tgCom = uiRoot.GetComponent<UnityEngine.UI.Toggle>();
            UGUIParser.SetRectTransform(LayerNode, tgCom);

            var bgCom = tgCom.targetGraphic as UnityEngine.UI.Image;
            if (bgCom != null)
            {
                UGUIParser.SetRectTransform(background, bgCom);
                bgCom.sprite = UGUIParser.LayerNode2Sprite(background, bgCom.type == UnityEngine.UI.Image.Type.Sliced);
            }

            var markCom = tgCom.graphic as UnityEngine.UI.Image;
            if (markCom != null)
            {
                UGUIParser.SetRectTransform(checkmark, markCom);
                markCom.sprite = UGUIParser.LayerNode2Sprite(checkmark, markCom.type == UnityEngine.UI.Image.Type.Sliced);
            }

            var textCom = tgCom.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (textCom != null)
            {
                textCom.gameObject.SetActive(label != null);
            }
            UGUIParser.SetTextStyle(label, textCom);
            UGUIParser.SetRectTransform(label, textCom, true, true, true);
        }
    }
}
#endif