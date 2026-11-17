// PSD Reader - 属性字典
// 嵌套的键值属性容器，支持点号路径访问

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PsdReader
{
    /// <summary>
    /// 属性字典 — 存储图层的附加资源/元数据。
    /// 支持嵌套访问：通过点号分隔路径自动遍历子字典/列表。
    /// 例如：props["TextEngine.EngineData.Editor.Text"]
    /// </summary>
    public class PropertyDictionary : IProperties
    {
        private readonly Dictionary<string, object> _dict = new Dictionary<string, object>();

        public object this[string property]
        {
            get
            {
                // 支持点号路径访问
                if (property.Contains('.'))
                    return GetNestedValue(property);
                return _dict.TryGetValue(property, out var val) ? val : null;
            }
        }

        public int Count => _dict.Count;

        public bool Contains(string property)
        {
            if (property.Contains('.'))
            {
                try { return GetNestedValue(property) != null; }
                catch { return false; }
            }
            return _dict.ContainsKey(property);
        }

        public void Add(string key, object value)
        {
            _dict[key] = value;
        }

        public bool TryGetValue(string key, out object value)
        {
            return _dict.TryGetValue(key, out value);
        }

        /// <summary>
        /// 获取布尔属性值
        /// </summary>
        public bool GetBool(string key, string subKey, bool defaultValue = false)
        {
            if (!Contains(key)) return defaultValue;
            var obj = this[key];
            if (obj is IProperties sub && sub.Contains(subKey))
            {
                var val = sub[subKey];
                if (val is bool b) return b;
                if (val is int i) return i != 0;
            }
            return defaultValue;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 沿点号路径递归查找嵌套属性
        /// </summary>
        private object GetNestedValue(string path)
        {
            string[] parts = path.Split('.');
            object current = _dict;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                if (current is IDictionary<string, object> dict)
                {
                    current = dict.TryGetValue(part, out var v) ? v : null;
                }
                else if (current is IProperties props)
                {
                    current = props.Contains(part) ? props[part] : null;
                }
                else if (current is IList list && int.TryParse(part, out int idx))
                {
                    current = idx < list.Count ? list[idx] : null;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }
    }
}
