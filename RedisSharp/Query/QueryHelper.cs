using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;

namespace RedisSharp
{
    public class QueryHelper
    {
        public static string TextSearch(string propertyName, string value, int fuzzyLevel = 0)
        {
            if (fuzzyLevel < 0 || fuzzyLevel > 3)
                throw new ArgumentException("Fuzzy level must be between 0 and 3.");

            string escapedValue = EscapeSpecialCharacters(value);
            if (escapedValue.Length > 3)
                escapedValue = $"{escapedValue}*";
            else if (escapedValue.Length > 10)
                escapedValue = $"*{escapedValue}*";

            string fuzzyValue = new string('%', fuzzyLevel) + escapedValue + new string('%', fuzzyLevel);
            return $"@{propertyName}:'{fuzzyValue}'";
        }
         
        public static string TextExact(string propertyName, string value)
        {
            // Escaped exact match query with Redis syntax for exact phrases
            string escapedValue = EscapeSpecialCharacters(value);
            return $"@{propertyName}:'\"{escapedValue}\"'";
        }

        public static string Tag(string propertyName, object value)
        {
            string escapedValue = EscapeSpecialCharacters(value.ToString());
            return $"@{propertyName}:{{{escapedValue}}}";
        }

        public static string Numeric(string propertyName, double? min = null, double? max = null)
        {
            var minValue = min?.ToString() ?? "-inf";
            var maxValue = max?.ToString() ?? "+inf";

            return $"@{propertyName}:[{minValue} {maxValue}]";
        }

        // Robust escape helper for Redis special characters
        private static string EscapeSpecialCharacters(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return input
                .Replace("\\", "\\\\")
                .Replace("-", "\\-")
                .Replace(":", "\\:")
                .Replace("\"", "\\\"")
                .Replace("'", "\\'")
                .Replace(".", "\\.")
                .Replace(",", "\\,")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("|", "\\|")
                .Replace("&", "\\&")
                .Replace("~", "\\~")
                .Replace("!", "\\!")
                .Replace("*", "\\*")
                .Replace("?", "\\?")
                .Replace("^", "\\^")
                .Replace("$", "\\$")
                .Replace("@", "\\@");
        }
    }
}
