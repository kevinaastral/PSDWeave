// PSD Reader - EngineData (PostScript-like) 格式解析器
// 解析 TySh descriptor 中 EngineData (tdta) 的二进制数据
// 格式类似 PostScript: << /key value >> 字典, [ ] 数组, (string) 字符串, /key 键名

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PsdReader
{
    /// <summary>
    /// 解析 PSD EngineData 二进制格式（PostScript-like token stream）→ PropertyDictionary
    /// </summary>
    internal static class EngineDataParser
    {
        /// <summary>
        /// 解析 EngineData 二进制数据为 PropertyDictionary
        /// </summary>
        public static PropertyDictionary Parse(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            var tokens = Tokenize(data);
            int pos = 0;
            var result = ParseValue(tokens, ref pos);
            return result as PropertyDictionary;
        }

        #region Tokenizer

        private enum TokenType
        {
            DictStart,   // <<
            DictEnd,     // >>
            ArrayStart,  // [
            ArrayEnd,    // ]
            Key,         // /KeyName
            String,      // (string content)
            Number,      // integer or float
            Boolean,     // true / false
        }

        private struct Token
        {
            public TokenType Type;
            public string Value;
        }

        private static List<Token> Tokenize(byte[] data)
        {
            var tokens = new List<Token>();
            int i = 0;
            int len = data.Length;

            while (i < len)
            {
                byte b = data[i];

                // 跳过空白
                if (b == ' ' || b == '\t' || b == '\r' || b == '\n' || b == 0)
                {
                    i++;
                    continue;
                }

                // << 字典开始
                if (b == '<' && i + 1 < len && data[i + 1] == '<')
                {
                    tokens.Add(new Token { Type = TokenType.DictStart });
                    i += 2;
                    continue;
                }

                // >> 字典结束
                if (b == '>' && i + 1 < len && data[i + 1] == '>')
                {
                    tokens.Add(new Token { Type = TokenType.DictEnd });
                    i += 2;
                    continue;
                }

                // [ 数组开始
                if (b == '[')
                {
                    tokens.Add(new Token { Type = TokenType.ArrayStart });
                    i++;
                    continue;
                }

                // ] 数组结束
                if (b == ']')
                {
                    tokens.Add(new Token { Type = TokenType.ArrayEnd });
                    i++;
                    continue;
                }

                // /KeyName
                if (b == '/')
                {
                    i++;
                    int start = i;
                    while (i < len && !IsDelimiter(data[i]))
                        i++;
                    tokens.Add(new Token { Type = TokenType.Key, Value = Encoding.ASCII.GetString(data, start, i - start) });
                    continue;
                }

                // (string) - 支持转义、嵌套括号和 UTF-16 BE 编码
                if (b == '(')
                {
                    i++;
                    // 先收集原始字节
                    var rawBytes = new List<byte>();
                    int depth = 1;
                    while (i < len && depth > 0)
                    {
                        byte c = data[i];
                        if (c == '\\' && i + 1 < len)
                        {
                            i++;
                            rawBytes.Add(data[i]);
                        }
                        else if (c == '(')
                        {
                            depth++;
                            rawBytes.Add(c);
                        }
                        else if (c == ')')
                        {
                            depth--;
                            if (depth > 0) rawBytes.Add(c);
                        }
                        else
                        {
                            rawBytes.Add(c);
                        }
                        i++;
                    }
                    // 检测 UTF-16 BE BOM (0xFE 0xFF) 并正确解码
                    string strValue;
                    if (rawBytes.Count >= 2 && rawBytes[0] == 0xFE && rawBytes[1] == 0xFF)
                    {
                        var arr = rawBytes.ToArray();
                        strValue = Encoding.BigEndianUnicode.GetString(arr, 2, arr.Length - 2);
                    }
                    else
                    {
                        var sb = new StringBuilder(rawBytes.Count);
                        foreach (var rb in rawBytes) sb.Append((char)rb);
                        strValue = sb.ToString();
                    }
                    tokens.Add(new Token { Type = TokenType.String, Value = strValue });
                    continue;
                }

                // 尝试读取 true/false/数字
                if (IsWordChar(b))
                {
                    int start = i;
                    while (i < len && IsWordChar(data[i]))
                        i++;
                    string word = Encoding.ASCII.GetString(data, start, i - start);

                    if (word == "true" || word == "false")
                    {
                        tokens.Add(new Token { Type = TokenType.Boolean, Value = word });
                    }
                    else
                    {
                        tokens.Add(new Token { Type = TokenType.Number, Value = word });
                    }
                    continue;
                }

                // 负号开头的数字
                if (b == '-' && i + 1 < len && (data[i + 1] >= '0' && data[i + 1] <= '9' || data[i + 1] == '.'))
                {
                    int start = i;
                    i++;
                    while (i < len && IsWordChar(data[i]))
                        i++;
                    string word = Encoding.ASCII.GetString(data, start, i - start);
                    tokens.Add(new Token { Type = TokenType.Number, Value = word });
                    continue;
                }

                // 未知字符跳过
                i++;
            }

            return tokens;
        }

        private static bool IsDelimiter(byte b)
        {
            return b == ' ' || b == '\t' || b == '\r' || b == '\n' || b == 0
                || b == '<' || b == '>' || b == '[' || b == ']'
                || b == '(' || b == ')' || b == '/';
        }

        private static bool IsWordChar(byte b)
        {
            return (b >= '0' && b <= '9') || (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z')
                || b == '.' || b == '_' || b == '+';
        }

        #endregion

        #region Parser

        private static object ParseValue(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count)
                return null;

            var token = tokens[pos];

            switch (token.Type)
            {
                case TokenType.DictStart:
                    return ParseDict(tokens, ref pos);

                case TokenType.ArrayStart:
                    return ParseArray(tokens, ref pos);

                case TokenType.String:
                    pos++;
                    return token.Value;

                case TokenType.Boolean:
                    pos++;
                    return token.Value == "true";

                case TokenType.Number:
                    pos++;
                    if (token.Value.Contains('.'))
                    {
                        if (double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double d))
                            return d;
                    }
                    if (int.TryParse(token.Value, out int intVal))
                        return intVal;
                    if (double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double dv))
                        return dv;
                    return token.Value;

                default:
                    pos++;
                    return null;
            }
        }

        private static PropertyDictionary ParseDict(List<Token> tokens, ref int pos)
        {
            pos++; // skip <<
            var dict = new PropertyDictionary();

            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokenType.DictEnd)
                {
                    pos++; // skip >>
                    break;
                }

                if (tokens[pos].Type == TokenType.Key)
                {
                    string key = tokens[pos].Value;
                    pos++;
                    object value = ParseValue(tokens, ref pos);
                    dict.Add(key, value);
                }
                else
                {
                    // 意外token，跳过
                    pos++;
                }
            }

            return dict;
        }

        private static ArrayList ParseArray(List<Token> tokens, ref int pos)
        {
            pos++; // skip [
            var list = new ArrayList();

            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokenType.ArrayEnd)
                {
                    pos++; // skip ]
                    break;
                }

                object value = ParseValue(tokens, ref pos);
                if (value != null)
                    list.Add(value);
            }

            return list;
        }

        #endregion
    }
}
