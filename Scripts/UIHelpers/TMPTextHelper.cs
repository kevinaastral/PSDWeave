/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class TMPTextHelper : UIHelperBase
    {
        [SerializeField] PsdLayerNode text;

        public override PsdLayerNode[] GetDependencies()
        {
            return CalculateDependencies(text);
        }

        public override void ParseAndAttachUIElements()
        {
            if (LayerNode.LayerType == PsdLayerType.TextLayer)
            {
                text = LayerNode;
            }
            else
            {
                LayerNode.SetUIType(UGUIParser.Instance.DefaultImage);
            }
        }

        protected override void InitUIElements(GameObject uiRoot)
        {
            var textCom = uiRoot.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            UGUIParser.SetTextStyle(text, textCom);
            UGUIParser.SetRectTransform(text, textCom);
            textCom.raycastTarget = false;
        }
    }
}
#endif