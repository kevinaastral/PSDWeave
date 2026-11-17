/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class InputFieldHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode background;
        [SerializeField] PsdLayerNode placeholder;
        [SerializeField] PsdLayerNode text;
        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, placeholder, text);
        }

        public override void ParseAndAttachUIElements()
        {
            background = LayerNode.FindSubLayerNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
            placeholder = LayerNode.FindSubLayerNode(GUIType.InputField_Placeholder);
            text = LayerNode.FindSubLayerNode(GUIType.InputField_Text, GUIType.Text);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var input = uiRoot.GetComponent<InputField>();
            UGUIParser.SetRectTransform(background, input);

            var bgImage = input.targetGraphic as Image;
            bgImage.sprite = UGUIParser.LayerNode2Sprite(background, bgImage.type == Image.Type.Sliced);
            UGUIParser.SetRectTransform(placeholder, input.placeholder);
            UGUIParser.SetRectTransform(text, input.textComponent);
            UGUIParser.SetTextStyle(placeholder, input.placeholder as Text);
            var textInfo = UGUIParser.SetTextStyle(text, input.textComponent);
            input.text = textInfo.Text;
        }
    }
}
#endif