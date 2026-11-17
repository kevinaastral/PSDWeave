using System;
using System.Collections.Generic;

namespace UGF.EditorTools.Psd2UGUI.Smartexport
{
    [Serializable]
    public sealed class PsdLayerManifest
    {
        public string schema;
        public string version;
        public PsdManifestDocument psd;
        public string document;
        public string module;
        public string generatedAt;
        public PsdManifestNaming naming;
        public PsdManifestImageProcess[] imageProcessing;
        public PsdManifestLayer[] layers;
        public PsdManifestLayer[] components;

        public PsdManifestLayer[] GetLayers()
        {
            return layers != null && layers.Length > 0 ? layers : components ?? new PsdManifestLayer[0];
        }

        /// <summary>
        /// Returns only layers that still exist as independently renderable
        /// nodes after Photoshop-side visual processing.  Clipping members are
        /// kept in the raw manifest as provenance, but must not participate in
        /// Unity name/flat-index matching after they have been merged into the
        /// canonical clipping base.
        /// </summary>
        public PsdManifestLayer[] GetRenderableLayers()
        {
            var sourceLayers = GetLayers();
            if (sourceLayers.Length == 0 || imageProcessing == null || imageProcessing.Length == 0)
                return sourceLayers;

            var supersededLayerIds = new HashSet<int>();
            foreach (var process in imageProcessing)
            {
                if (process == null || process.layerId == 0 || process.resultLayerId == 0)
                    continue;
                if (!string.Equals(process.action, "merge_clipping", StringComparison.OrdinalIgnoreCase))
                    continue;
                supersededLayerIds.Add(process.layerId);
            }

            if (supersededLayerIds.Count == 0)
                return sourceLayers;

            var renderableLayers = new List<PsdManifestLayer>(sourceLayers.Length);
            foreach (var layer in sourceLayers)
            {
                if (layer == null || !supersededLayerIds.Contains(layer.LayerId))
                    renderableLayers.Add(layer);
            }
            return renderableLayers.ToArray();
        }
    }

    [Serializable]
    public sealed class PsdManifestImageProcess
    {
        public int layerId;
        public string name;
        public string path;
        public string action;
        public string detail;
        public string severity;
        public int resultLayerId;
    }

    [Serializable]
    public sealed class PsdManifestDocument
    {
        public string name;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class PsdManifestNaming
    {
        public string module;
        public string ruleSetVersion;
    }

    [Serializable]
    public sealed class PsdManifestLayer
    {
        public int id;
        public int layerId;
        public int flatIndex = -1;
        public string path;
        public string name;
        public string originalName;
        public string proposedName;
        public string lockedName;
        public string type;
        public string kind;
        public string psd2uguiTag;
        public bool isComponent;
        public bool shouldBind;
        public float confidence;
        public PsdManifestBinding binding;
        public int[] bounds;
        public int[] rect;
        public string text;
        public string textSample;
        public string[] issues;

        public int LayerId => id != 0 ? id : layerId;
        public string Kind => string.IsNullOrEmpty(kind) ? type : kind;
        public string DisplayName => string.IsNullOrEmpty(proposedName) ? name : proposedName;
        public string Text => string.IsNullOrEmpty(text) ? textSample : text;
        public bool ShouldBind => binding != null ? binding.shouldBind : shouldBind;
        public string SuggestedProp => binding != null && !string.IsNullOrEmpty(binding.suggestedProp)
            ? binding.suggestedProp
            : DisplayName;
        public string BindingCategory => binding != null ? binding.category : null;
    }

    [Serializable]
    public sealed class PsdManifestBinding
    {
        public bool shouldBind;
        public string suggestedProp;
        public string category;
    }

    [Serializable]
    public sealed class PsdManifestIssueReport
    {
        public string manifestPath;
        public string prefabPath;
        public string generatedAt;
        public PsdManifestIssue[] issues;
    }

    [Serializable]
    public sealed class PsdManifestIssue
    {
        public string severity;
        public string code;
        public string message;
        public string nodePath;
        public int flatIndex = -1;
        public int layerId;
    }
}
