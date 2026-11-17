/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class TMPInputFieldHelper : UIHelperBase
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
            text = LayerNode.FindSubLayerNode(GUIType.InputField_Text, GUIType.TMPText);
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var input = uiRoot.GetComponent<TMP_InputField>();
            if (input == null)
            {
                Debug.LogWarning($"TMPInputField缺少TMP_InputField组件, 已跳过初始化: {uiRoot.name}");
                return;
            }

            UGUIParser.SetRectTransform(background, input);

            var bgImage = input.targetGraphic as Image ?? uiRoot.GetComponent<Image>();
            if (bgImage != null)
            {
                input.targetGraphic = bgImage;
                var bgSprite = UGUIParser.LayerNode2Sprite(background, bgImage.type == Image.Type.Sliced);
                if (bgSprite != null)
                {
                    bgImage.sprite = bgSprite;
                }
            }

            var placeholderText = input.placeholder as TextMeshProUGUI;
            if (placeholderText == null)
            {
                placeholderText = uiRoot.transform.Find("Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>();
                if (placeholderText != null)
                {
                    input.placeholder = placeholderText;
                }
            }

            var contentText = input.textComponent as TextMeshProUGUI;
            if (contentText == null)
            {
                contentText = uiRoot.transform.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
                if (contentText != null)
                {
                    input.textComponent = contentText;
                }
            }

            UGUIParser.SetRectTransform(placeholder, placeholderText);
            UGUIParser.SetRectTransform(text, contentText);
            UGUIParser.SetTextStyle(placeholder, placeholderText);
            UGUIParser.SetTextStyle(text, contentText);

            if (text != null && text.ParseTextLayerInfo(out var textInfo))
            {
                input.text = textInfo.Text ?? string.Empty;
            }
        }
    }
}
#endif
