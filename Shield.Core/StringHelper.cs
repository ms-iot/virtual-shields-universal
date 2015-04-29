using System;
using System.Collections.Generic;

namespace Shield.Core
{
    public static class StringHelper
    {
        public static string LeftOf(this string source, string separator)
        {
            var index = source.IndexOf(separator, StringComparison.Ordinal);
            if (index == -1)
            {
                return null;
            }

            return source.Substring(0, index);
        }

        public static string RightOf(this string source, string separator)
        {
            var index = source.IndexOf(separator, StringComparison.Ordinal);
            if (index == -1)
            {
                return null;
            }

            return source.Substring(index + separator.Length);
        }

        public static string[] RemoveQuotes(this string[] source)
        {
            var items = new string[source.Length];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = source[i].Length > 2 ? source[i].Substring(1, source[i].Length - 2) : string.Empty;
            }

            return items;
        }

        public static string[] SplitBy(this string source, string left, string right = null)
        {
            var sets = ("XXX" + source).Split(new[] { left, right }, StringSplitOptions.None);
            var items = new List<string>();
            for (int i = 1; i < sets.Length; i += 2)
            {
                items.Add(sets[i]);
            }

            return items.ToArray();
        }

        public static string Between(this string source, string leftSeparator, string rightSeparator = null)
        {
            rightSeparator = rightSeparator ?? leftSeparator;
            return source.RightOf(leftSeparator).LeftOf(rightSeparator);
        }
    }
}