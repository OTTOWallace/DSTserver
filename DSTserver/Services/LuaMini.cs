using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DSTserver.Services;

/// <summary>
/// 极简 Lua 字面量解析器：足够解析 modinfo.lua / modoverrides.lua / worldgenoverride.lua。
/// 支持：字符串、数字、布尔、nil、表（含命名键与位置项）、-- 行注释、--[[ ]] 块注释。
/// </summary>
public static class LuaMini
{
    public static string StripComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        int i = 0, n = s.Length;
        while (i < n)
        {
            if (i + 1 < n && s[i] == '-' && s[i + 1] == '-')
            {
                if (i + 3 < n && s[i + 2] == '[' && s[i + 3] == '[')
                {
                    int end = s.IndexOf("]]", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? n : end + 2;
                }
                else
                {
                    int end = s.IndexOf('\n', i);
                    i = end < 0 ? n : end + 1;
                }
                continue;
            }
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    private static bool IsWs(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';
    private static bool IsDigit(char c) => c >= '0' && c <= '9';
    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && IsWs(s[i])) i++;
    }

    public static object? ParseValue(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) return null;
        char c = s[i];

        if (c == '"' || c == '\'') return ParseString(s, ref i);
        if (c == '-' || IsDigit(c)) return ParseNumber(s, ref i);
        if (c == '{') return ParseTable(s, ref i);

        if (IsIdentStart(c))
        {
            int start = i;
            while (i < s.Length && IsIdentPart(s[i])) i++;
            string word = s.Substring(start, i - start);
            return word switch
            {
                "true" => true,
                "false" => false,
                "nil" => null,
                _ => word,
            };
        }
        i++;
        return null;
    }

    public static string ParseString(string s, ref int i)
    {
        char quote = s[i];
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char e = s[i + 1];
                sb.Append(e switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => e,
                });
                i += 2;
                continue;
            }
            if (c == quote) { i++; return sb.ToString(); }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    public static double ParseNumber(string s, ref int i)
    {
        int start = i;
        if (i < s.Length && s[i] == '-') i++;
        while (i < s.Length && (IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
               ((s[i] == '+' || s[i] == '-') && i > start && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
        {
            i++;
        }
        if (double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return v;
        return 0;
    }

    /// <summary>
    /// 解析 { ... } 表。字符串键保留原样；位置项用索引 "1","2"... 作键。
    /// </summary>
    public static Dictionary<string, object?> ParseTable(string s, ref int i)
    {
        var result = new Dictionary<string, object?>();
        i++; // 跳过 '{'
        int index = 0;
        while (true)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) break;
            if (s[i] == '}') { i++; break; }

            string? key = null;
            bool hasKey = false;

            if (s[i] == '[')
            {
                i++;
                object? k = ParseValue(s, ref i);
                key = k?.ToString();
                hasKey = true;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') i++;
            }
            else if (IsIdentStart(s[i]))
            {
                int start = i;
                while (i < s.Length && IsIdentPart(s[i])) i++;
                key = s.Substring(start, i - start);
                hasKey = true;
            }

            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '=')
            {
                i++;
                object? value = ParseValue(s, ref i);
                if (key != null) result[key] = value;
                else result[(index + 1).ToString()] = value;
            }
            else
            {
                object? val;
                if (hasKey) val = key;           // 表内裸标识符，按字符串处理
                else val = ParseValue(s, ref i); // 位置项（数字 / 字符串 / 嵌套表）
                index++;
                result[index.ToString()] = val;
            }

            SkipWs(s, ref i);
            if (i < s.Length && (s[i] == ',' || s[i] == ';')) { i++; continue; }
        }
        return result;
    }

    /// <summary>在文本里查找 `key = value` 赋值并解析 value（自动剥离注释）。</summary>
    public static object? FindAssignment(string s, string key)
    {
        s = StripComments(s);
        int i = 0, n = s.Length;
        while (i < n)
        {
            char c = s[i];
            if (c == '"' || c == '\'') { ParseString(s, ref i); continue; }
            if (IsIdentStart(c))
            {
                int start = i;
                while (i < n && IsIdentPart(s[i])) i++;
                string word = s.Substring(start, i - start);
                if (word == key)
                {
                    SkipWs(s, ref i);
                    if (i < n && s[i] == '=')
                    {
                        i++;
                        return ParseValue(s, ref i);
                    }
                }
                continue;
            }
            i++;
        }
        return null;
    }
}
