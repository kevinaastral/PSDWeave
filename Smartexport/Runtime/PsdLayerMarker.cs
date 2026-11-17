using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI.Smartexport
{
    [DisallowMultipleComponent]
    public sealed class PsdLayerMarker : MonoBehaviour
    {
        public int PsdLayerId;
        public int FlatIndex = -1;
        public string HierarchyPath;
        public string OriginalName;
        public string ProposedName;
        public string LockedName;
        public string Kind;
        public string Psd2UguiTag;
        public bool ShouldBind;
        public bool ManualBindingOverride;
        public string SuggestedProp;
        public string BindingCategory;
        [HideInInspector] public bool FollowsSource;
        public float Confidence;
    }
}
