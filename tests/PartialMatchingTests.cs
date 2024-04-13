using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.SRM.Tests
{
    public class Strbum
    {
        public string Text {  get; set; }
        public int Index { get; set; }
        public bool IsEnd => Index >= Text.Length;
        public char Current => Text[Index];

        public Strbum(string text)
        {
            Text = text;
        }

        public void Next() => Index++;
    }

    [TestClass]
    public class PartialMatchingTests
    {
        [TestMethod]
        public void Matches123()
        {
            // Number
            // @"\b(0b[01']+)"
            // @"(-?)\b([\d']+(\.[\d']*)?|\.[\d']+)(u|U|l|L|ul|UL|f|F|b|B)"
            // @"(-?)(\b0[xX][a-fA-F0-9']+|(\b[\d']+(\.[\d']*)?|\.[\d']+)([eE][-+]?[\d']+)?)"
            RegexTree tree1 = RegexParser.Parse(@"(0b[01']+)", RegexOptions.IgnoreCase);
            LeMatch(tree1.root, new Strbum("0b01"));

            RegexTree tree = RegexParser.Parse("a(b*|c*)", RegexOptions.IgnoreCase);
            LeMatch(tree.root, new Strbum("abc"));
            //tree.root.Type
            //tree.root._ch;
        }

        public bool LeMatch(RegexNode node, Strbum str)
        {
            switch (node.Type)
            {
                case RegexNodeType.Capture:
                    return LeMatch(node.children.First(), str);
                case RegexNodeType.Concatenate: // sequence of stuff
                    foreach (var child in node.children) 
                    {
                        if (!LeMatch(child, str)) return false;
                    }
                    return true;
                case RegexNodeType.One:
                    if (str.IsEnd ||
                        str.Current != node.oneChar) return false;
                    str.Next();
                    return true;
                case RegexNodeType.Alternate:
                    return node.children.Any(child => LeMatch(child, str));
                case RegexNodeType.Oneloop:
                    for (var i = 0; i < node.maxIterations; i++)
                    {
                        if (str.IsEnd || str.Current != node.oneChar)
                        { 
                            return (i >= node.minIterations);
                        }

                        str.Next();
                    }
                    return true;

                case RegexNodeType.Multi:
                    foreach (var c in node._str)
                    {
                        if (str.IsEnd || c != str.Current) return false;
                        str.Next();
                    }
                    return true;

                case RegexNodeType.Set:
                    if (str.IsEnd) return false;
                    var cats = node.DecodedSet;
                    var contains = cats.ranges.Any(range => range.Contains(str.Current));
                    if (contains && !cats.Negate ||
                        !contains & cats.Negate)
                    {
                        str.Next();
                        return true;
                    }
                    return false;
                case RegexNodeType.Setloop:
                    var cats1 = node.DecodedSet;
                    for (var j = 0; j < node.maxIterations; j++)
                    {
                        if (!str.IsEnd)
                        {
                            var contains1 = cats1.ranges.Any(range => range.Contains(str.Current));
                            if (contains1 && !cats1.Negate ||
                                !contains1 & cats1.Negate)
                            {
                                str.Next();
                                continue;
                            }
                        }
                        return (j >= node.minIterations);
                    }
                    return true;
                case RegexNodeType.Setlazy:
                    var cats2 = node.DecodedSet;
                    return false;
                default:
                    return false;
            }
        }


        [TestMethod]
        public void MatchesABC()
        {
            var sr = new Regex("a(b*|c*)", RegexOptions.IgnoreCase);
            var input = "ab";

            var lematch = sr.PartialMatch(input);

            //Func<int, int, Match> m = (x, y) => new Match(x, y);
            //var expectedMatches = new Match[] { m(3, 3), m(12, 3), m(15, 3) };
            //var matches = sr.Matches(input).ToList();
            //CollectionAssert.AreEqual(expectedMatches, matches);
        }
    }
}
