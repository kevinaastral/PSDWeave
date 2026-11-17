/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class TMPButtonHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode background = null;
        [SerializeField] PsdLayerNode text = null;
        [Header("Sprite Swap:")]
        [SerializeField] PsdLayerNode highlight = null;
        [SerializeField] PsdLayerNode press = null;
        [SerializeField] PsdLayerNode select = null;
        [SerializeField] PsdLayerNode disable = null;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(background, text, highlight, press, select, disable);
        }

        public override void ParseAndAttachUIElements()
        {
            if (LayerNode.LayerType == PsdLayerType.LayerGroup)
            {
                background = LayerNode.FindSubLayerNode(GUIType.Background, GUIType.Image, GUIType.RawImage);
                text = LayerNode.FindSubLayerNode(GUIType.Button_Text, GUIType.TMPText, GUIType.Text);
                highlight = LayerNode.FindSubLayerNode(GUIType.Button_Highlight);
                press = LayerNode.FindSubLayerNode(GUIType.Button_Press);
                select = LayerNode.FindSubLayerNode(GUIType.Button_Select);
                disable = LayerNode.FindSubLayerNode(GUIType.Button_Disable);
            }
            else
            {
                background = LayerNode;
                text = null;
                highlight = null;
                press = null;
                select = null;
                disable = null;
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var button = uiRoot.GetComponent<Button>();
            var btImg = button.GetComponent<Image>();
            bool useSliceSp = btImg.type == Image.Type.Sliced;
            btImg.sprite = UGUIParser.LayerNode2Sprite(background, useSliceSp) ?? btImg.sprite;
            UGUIParser.SetRectTransform(LayerNode, button);
            var textCom = uiRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
            {
                if (textCom != null)
                {
                    DestroyImmediate(textCom.gameObject);
                }
            }
            else
            {
                textCom = textCom ?? CreateTemplateText(uiRoot);
                UGUIParser.SetTextStyle(text, textCom);
                UGUIParser.SetRectTransform(text, textCom);
            }

            bool useSpriteSwap = highlight != null || press != null || select != null || disable != null;
            button.transition = useSpriteSwap ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;
            if (button.transition == Selectable.Transition.SpriteSwap)
            {
                var spState = new SpriteState();
                spState.highlightedSprite = UGUIParser.LayerNode2Sprite(highlight, useSliceSp);
                spState.pressedSprite = UGUIParser.LayerNode2Sprite(press, useSliceSp);
                spState.selectedSprite = UGUIParser.LayerNode2Sprite(select, useSliceSp);
                spState.disabledSprite = UGUIParser.LayerNode2Sprite(disable, useSliceSp);
                button.spriteState = spState;
            }
        }

        private TextMeshProUGUI CreateTemplateText(GameObject uiRoot)
        {
            var prefab = UGUIParser.Instance?.GetRule(LayerNode.UIType)?.UIPrefab;
            var prefabText = prefab != null ? prefab.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (prefabText == null) return null;

            var textGo = Instantiate(prefabText.gameObject, uiRoot.transform, false);
            textGo.name = prefabText.gameObject.name;
            textGo.transform.SetAsLastSibling();
            return textGo.GetComponent<TextMeshProUGUI>();
        }
    }
}
#endif
