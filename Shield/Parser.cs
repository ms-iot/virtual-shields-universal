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
namespace Shield
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    using HtmlAgilityPack;

    using Newtonsoft.Json.Linq;

    using Shield.Core;

    using Windows.Data.Xml.Dom;

    using XmlDocument = Windows.Data.Xml.Dom.XmlDocument;
    using XmlNodeList = Windows.Data.Xml.Dom.XmlNodeList;

    public class Parser
    {
        private XmlDocument doc;

        private string keypart;

        private string valuepart;

        private XmlNodeList nodes;

        public string Content { get; set; }

        public string Instructions { get; set; }

        public Dictionary<string, string> Dictionary { get; } = new Dictionary<string, string>();

        public bool IsDictionary { get; set; }

        public string Tablename { get; set; }

        public string Result { get; set; }

        public bool Parse()
        {
            var temp = this.Content;

            var results = new List<string>();
            var potentialResults = new Stack<string>();
            var prefix = string.Empty;

            var parses = this.Instructions.Split('|');
            foreach (var parse in parses)
            {
                var colon = parse.IndexOf(':');
                if (colon > -1)
                {
                    var cmd = parse.Substring(0, colon);
                    var part = parse.Substring(colon + 1);
                    var save = cmd.Contains("?");
                    var nextToken = cmd.Contains("&");
                    var prefixed = cmd.Contains("+");
                    var restore = cmd.Contains("^");

                    if (nextToken)
                    {
                        results.Add(temp);
                        temp = string.Empty;
                    }

                    if (prefixed)
                    {
                        prefix += temp;
                        temp = string.Empty;
                    }

                    if (restore)
                    {
                        temp = potentialResults.Count > 0 ? potentialResults.Pop() : this.Content;
                    }

                    if (save)
                    {
                        potentialResults.Push(prefix + temp);
                        prefix = string.Empty;
                    }

                    var contains = temp.Contains(part);

                    if (cmd.Contains("i"))
                    {
                        if (!this.CheckDoc(temp))
                        {
                            return false;
                        }

                        this.nodes = this.doc.SelectNodes(part);
                        this.IsDictionary = true;
                    }
                    else if (cmd.Contains("k"))
                    {
                        this.keypart = part;
                    }
                    else if (cmd.Contains("v"))
                    {
                        this.valuepart = part;
                    }
                    else if (cmd.Contains("table") && this.nodes != null)
                    {
                        if (part.Contains("name="))
                        {
                            this.Tablename = part.Substring(part.IndexOf("name=") + 5);
                            var j = this.Tablename.IndexOf(";");
                            if (j > -1)
                            {
                                this.Tablename = this.Tablename.Substring(0, j);
                            }
                        }

                        foreach (var node in this.nodes)
                        {
                            IXmlNode key = null;
                            XmlNodeList valueset = null;
                            try
                            {
                                key = node.SelectSingleNode(this.keypart);
                                valueset = node.SelectNodes(this.valuepart);
                            }
                            catch (Exception)
                            {
                                // skip if not matching
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(key?.InnerText))
                            {
                                if (!string.IsNullOrWhiteSpace(key.InnerText)
                                    && !this.Dictionary.ContainsKey(key.InnerText))
                                {
                                    var values = new StringBuilder();
                                    var count = valueset.Count();
                                    foreach (var value in valueset)
                                    {
                                        values.Append(value.InnerText);
                                        if (--count > 0)
                                        {
                                            values.Append("~");
                                        }
                                    }

                                    this.Dictionary.Add(key.InnerText, values.ToString());
                                }
                            }
                        }

                        Debug.WriteLine("Table {0} saved count = {1}", this.Tablename, this.Dictionary.Count);
                    }
                    else if (cmd.Contains("R"))
                    {
                        temp = temp.RightOf(part);
                    }
                    else if (cmd.Contains("L"))
                    {
                        temp = temp.LeftOf(part);
                    }
                    else if (cmd.Contains("B"))
                    {
                        temp = temp.Between(part);
                    }
                    else if (cmd.Contains("!"))
                    {
                        temp = part;
                    }
                    else if (cmd.Contains("J") || cmd.Contains("j"))
                    {
                        var jParsed = JToken.Parse(temp);
                        if (jParsed != null)
                        {
                            var jToken = jParsed.SelectToken(part);
                            if (jToken != null)
                            {
                                if (cmd.Contains("j"))
                                {
                                    temp = ((JProperty)jToken).Name;
                                    if (cmd.Contains("J"))
                                    {
                                        temp += ":'" + jToken.Value<string>() + "'";
                                    }
                                }
                                else
                                {
                                    temp = jToken.Value<string>();
                                }
                            }
                            else
                            {
                                temp = string.Empty;
                            }
                        }
                        else
                        {
                            temp = string.Empty;
                        }
                    }
                    else if (cmd.Contains("X"))
                    {
                        var expr = new Regex(part);
                        var matches = expr.Match(part);
                        var matchResults = new StringBuilder();
                        while (matches.Success)
                        {
                            contains = true;
                            matchResults.Append(matches.Value);
                            matchResults.Append("~");
                            matches = matches.NextMatch();
                        }

                        temp = matchResults.ToString();
                        if (!string.IsNullOrWhiteSpace(temp))
                        {
                            temp = temp.Substring(0, temp.Length - 1);
                        }
                    }
                    else if (cmd.Contains("F"))
                    {
                        results.Add(prefix + temp);
                        prefix = string.Empty;
                        temp = string.Format(part, results.ToArray());
                        results.Clear();
                    }

                    if (save && !contains)
                    {
                        temp = potentialResults.Pop();
                    }
                }

                if (string.IsNullOrWhiteSpace(temp))
                {
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(temp))
            {
                results.Add(prefix + temp);
                prefix = string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < results.Count; i++)
            {
                builder.Append(results[i]);
                if (i < results.Count - 1)
                {
                    builder.Append("|");
                }
            }

            this.Result = builder.Length == 0 ? string.Empty : builder.ToString();

            return true;
        }

        public bool CheckDoc(string temp)
        {
            if (this.doc != null)
            {
                return true;
            }

            try
            {
                this.doc = new XmlDocument();
                this.doc.LoadXml(temp);
            }
            catch (Exception)
            {
                // backup leverage html agility pack
                try
                {
                    var hdoc = new HtmlDocument();
                    hdoc.LoadHtml(temp);
                    hdoc.OptionOutputAsXml = true;
                    hdoc.OptionAutoCloseOnEnd = true;

                    var stream = new MemoryStream();

                    var xtw = XmlWriter.Create(
                        stream, 
                        new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Fragment });

                    hdoc.Save(xtw);

                    stream.Position = 0;

                    this.doc.LoadXml((new StreamReader(stream)).ReadToEnd());
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }
    }
}