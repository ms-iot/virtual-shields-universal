/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

﻿using System;
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