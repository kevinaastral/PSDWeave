using System;
using System.Linq;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport.Editor
{
    internal static class ComponentBindingRules
    {
        private sealed class Rule
        {
            public readonly string Suffix;
            public readonly string[] Types;

            public Rule(string suffix, params string[] types)
            {
                Suffix = suffix;
                Types = types;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("@Btn", "UnityEngine.UI.Button"),
            new Rule("@Txt", "TMPro.TextMeshProUGUI", "UnityEngine.UI.Text"),
            new Rule("@Image", "UnityEngine.UI.Image"),
            new Rule("@RawImage", "UnityEngine.UI.RawImage"),
            new Rule("@Slider", "UnityEngine.UI.Slider"),
            new Rule("@Toggle", "UnityEngine.UI.Toggle"),
            new Rule("@Input", "TMPro.TMP_InputField", "UnityEngine.UI.InputField"),
            new Rule("@Dropdown", "TMPro.TMP_Dropdown", "UnityEngine.UI.Dropdown"),
            new Rule("@ScrollRect", "UnityEngine.UI.ScrollRect"),
            new Rule("@LoopVScroll", "UnityEngine.UI.LoopVerticalScrollRect"),
            new Rule("@LoopHScroll", "UnityEngine.UI.LoopHorizontalScrollRect"),
            new Rule("@LoopList", "SuperScrollView.LoopListView2"),
            new Rule("@LoopGrid", "SuperScrollView.LoopGridView"),
            new Rule("@Obj"),
            new Rule("@Tran", "UnityEngine.Transform"),
            new Rule("@Rect", "UnityEngine.RectTransform"),
            new Rule("@Canvas", "UnityEngine.Canvas"),
            new Rule("@CanvasGroup", "UnityEngine.CanvasGroup"),
            new Rule("@Animator", "UnityEngine.Animator"),
            new Rule("@Animation", "UnityEngine.Animation"),
            new Rule("@Spine", "Spine.Unity.SkeletonGraphic"),
        };

        public static bool TryGetSuffix(string name, out string suffix)
        {
            suffix = Rules.FirstOrDefault(rule => name.EndsWith(rule.Suffix, StringComparison.OrdinalIgnoreCase))?.Suffix;
            return suffix != null;
        }

        public static Component FindTarget(GameObject node, string suffix)
        {
            var components = node.GetComponents<Component>().Where(item => item != null).ToArray();
            var rule = Rules.FirstOrDefault(item => string.Equals(item.Suffix, suffix, StringComparison.OrdinalIgnoreCase));
            if (rule == null)
                return null;

            foreach (var type in rule.Types)
            {
                var match = components.FirstOrDefault(component => MatchesType(component.GetType(), type));
                if (match != null)
                    return match;
            }

            return null;
        }

        private static bool MatchesType(Type type, string fullName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.FullName == fullName)
                    return true;
            }

            return false;
        }
    }
}
