using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PsdTmpTextEffectBinder : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI sourceText;
        [SerializeField] private TextMeshProUGUI glowText;
        // Kept for prefabs exported before multiple outside strokes were supported.
        [SerializeField] private TextMeshProUGUI strokeText;
        [SerializeField] private List<TextMeshProUGUI> strokeTexts = new List<TextMeshProUGUI>();
        [SerializeField] private TextMeshProUGUI shadowText;
        [SerializeField] private Vector2 shadowOffset;

        public void Configure(TextMeshProUGUI source, TextMeshProUGUI glow, TextMeshProUGUI stroke)
        {
            Configure(source, glow, stroke != null ? new List<TextMeshProUGUI> { stroke } : null, null, Vector2.zero);
        }

        public void Configure(TextMeshProUGUI source, TextMeshProUGUI glow, IList<TextMeshProUGUI> strokes, TextMeshProUGUI shadow, Vector2 offset)
        {
            sourceText = source != null ? source : GetComponent<TextMeshProUGUI>();
            glowText = glow;
            strokeTexts ??= new List<TextMeshProUGUI>();
            strokeTexts.Clear();
            if (strokes != null)
            {
                foreach (var stroke in strokes)
                {
                    if (stroke != null && !strokeTexts.Contains(stroke))
                    {
                        strokeTexts.Add(stroke);
                    }
                }
            }

            strokeText = strokeTexts.Count > 0 ? strokeTexts[0] : null;
            shadowText = shadow;
            shadowOffset = offset;
            SyncNow();
        }

        private void OnEnable()
        {
            SetTargetsActive(true);
            SyncNow();
        }

        private void OnDisable()
        {
            SetTargetsActive(false);
        }

        private void OnValidate()
        {
            if (sourceText == null)
                sourceText = GetComponent<TextMeshProUGUI>();
            strokeTexts ??= new List<TextMeshProUGUI>();
            SyncNow();
        }

        private void LateUpdate()
        {
            SyncNow();
        }

        public void SyncNow()
        {
            if (sourceText == null)
                sourceText = GetComponent<TextMeshProUGUI>();
            if (sourceText == null)
                return;

            SyncTarget(glowText, true, Vector2.zero);
            foreach (var stroke in EnumerateStrokeTargets())
                SyncTarget(stroke, false, Vector2.zero);
            SyncTarget(shadowText, false, shadowOffset);
            SyncEffectOrder();
        }

        private IEnumerable<TextMeshProUGUI> EnumerateStrokeTargets()
        {
            var emitted = new HashSet<TextMeshProUGUI>();
            if (strokeTexts != null)
            {
                foreach (var stroke in strokeTexts)
                {
                    if (stroke != null && emitted.Add(stroke))
                        yield return stroke;
                }
            }

            if (strokeText != null && emitted.Add(strokeText))
                yield return strokeText;
        }

        private void SyncTarget(TextMeshProUGUI target, bool syncColor, Vector2 positionOffset)
        {
            if (target == null || target == sourceText)
                return;

            if (!target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
            if (target.enabled != sourceText.enabled)
                target.enabled = sourceText.enabled;

            if (target.font != sourceText.font) target.font = sourceText.font;
            if (target.text != sourceText.text) target.text = sourceText.text;
            if (!Mathf.Approximately(target.fontSize, sourceText.fontSize)) target.fontSize = sourceText.fontSize;
            if (target.fontStyle != sourceText.fontStyle) target.fontStyle = sourceText.fontStyle;
            if (target.fontWeight != sourceText.fontWeight) target.fontWeight = sourceText.fontWeight;
            if (!Mathf.Approximately(target.characterSpacing, sourceText.characterSpacing)) target.characterSpacing = sourceText.characterSpacing;
            if (!Mathf.Approximately(target.lineSpacing, sourceText.lineSpacing)) target.lineSpacing = sourceText.lineSpacing;
            if (target.enableAutoSizing != sourceText.enableAutoSizing) target.enableAutoSizing = sourceText.enableAutoSizing;
            if (target.enableWordWrapping != sourceText.enableWordWrapping) target.enableWordWrapping = sourceText.enableWordWrapping;
            if (target.extraPadding != sourceText.extraPadding) target.extraPadding = sourceText.extraPadding;
            if (target.overflowMode != sourceText.overflowMode) target.overflowMode = sourceText.overflowMode;
            if (target.alignment != sourceText.alignment) target.alignment = sourceText.alignment;
            if (target.verticalAlignment != sourceText.verticalAlignment) target.verticalAlignment = sourceText.verticalAlignment;
            if (target.horizontalAlignment != sourceText.horizontalAlignment) target.horizontalAlignment = sourceText.horizontalAlignment;
            if (target.margin != sourceText.margin) target.margin = sourceText.margin;
            if (target.richText != sourceText.richText) target.richText = sourceText.richText;
            if (syncColor && target.color != sourceText.color) target.color = sourceText.color;
            if (target.raycastTarget) target.raycastTarget = false;

            SyncRectTransform(sourceText.rectTransform, target.rectTransform, positionOffset);
        }

        private void SetTargetsActive(bool active)
        {
            SetTargetActive(glowText, active);
            foreach (var stroke in EnumerateStrokeTargets())
                SetTargetActive(stroke, active);
            SetTargetActive(shadowText, active);
        }

        private static void SetTargetActive(TextMeshProUGUI target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }

        private static void SyncRectTransform(RectTransform source, RectTransform target, Vector2 positionOffset)
        {
            if (source == null || target == null)
                return;

            if (target.anchorMin != source.anchorMin) target.anchorMin = source.anchorMin;
            if (target.anchorMax != source.anchorMax) target.anchorMax = source.anchorMax;
            if (target.pivot != source.pivot) target.pivot = source.pivot;
            if (target.sizeDelta != source.sizeDelta) target.sizeDelta = source.sizeDelta;
            var position = source.anchoredPosition3D + new Vector3(positionOffset.x, positionOffset.y, 0f);
            if (target.anchoredPosition3D != position) target.anchoredPosition3D = position;
            if (target.localRotation != source.localRotation) target.localRotation = source.localRotation;
            if (target.localScale != source.localScale) target.localScale = source.localScale;
        }

        private void SyncEffectOrder()
        {
            var sourceTransform = sourceText != null ? sourceText.transform : null;
            if (sourceTransform == null)
                return;

            var orderedStrokes = new List<TextMeshProUGUI>(EnumerateStrokeTargets());
            Transform anchor = sourceTransform;
            for (int i = orderedStrokes.Count - 1; i >= 0; i--)
            {
                var stroke = orderedStrokes[i] != null ? orderedStrokes[i].transform : null;
                MoveSiblingBefore(stroke, anchor);
                anchor = stroke ?? anchor;
            }

            if (shadowText != null)
            {
                MoveSiblingBefore(shadowText.transform, anchor);
                anchor = shadowText.transform;
            }

            MoveSiblingBefore(glowText != null ? glowText.transform : null, anchor);
        }

        private static void MoveSiblingBefore(Transform child, Transform target)
        {
            if (child == null || target == null || child.parent != target.parent)
                return;

            int targetIndex = target.GetSiblingIndex();
            if (child.GetSiblingIndex() < targetIndex)
                targetIndex--;
            targetIndex = Mathf.Clamp(targetIndex, 0, child.parent.childCount - 1);
            if (child.GetSiblingIndex() != targetIndex)
                child.SetSiblingIndex(targetIndex);
        }
    }
}
