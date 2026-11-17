#if UNITY_EDITOR
/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public sealed class PsdGeneratedKey : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        private string m_Key = null;

        [SerializeField]
        [HideInInspector]
        private string m_TypeKey = null;

        [SerializeField]
        [HideInInspector]
        private bool m_IsContainer = false;

        public string Key
        {
            get => m_Key ?? string.Empty;
            set => m_Key = value;
        }

        public string TypeKey
        {
            get => m_TypeKey ?? string.Empty;
            set => m_TypeKey = value;
        }

        public bool IsContainer
        {
            get => m_IsContainer;
            set => m_IsContainer = value;
        }

        public string CompositeKey => $"{(m_IsContainer ? "C" : "N")}:{TypeKey}:{Key}";
    }
}
#endif
