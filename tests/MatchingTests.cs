using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Linq;

namespace Microsoft.SRM.Tests
{
    [TestClass]
    public class SymbolicRegexTests
    {
        private void AssertIsMatchesAgree(System.Text.RegularExpressions.Regex r, Regex sr, string input, bool expected)
        {
            Assert.AreEqual(expected, r.IsMatch(input), $"Unexpected result. Pattern:{r} Input:{input}");
            Assert.AreEqual(r.IsMatch(input), sr.IsMatch(input), $"Mismatch with System.Text.RegularExpressions. Pattern:{r} Input:{input}");
        }

        [TestMethod]
        public void IsMatch1()
        {
            var pattern = @"^\w\d\w{1,8}$";
            var r = new System.Text.RegularExpressions.Regex(pattern);
            var sr = new Regex(pattern);
            AssertIsMatchesAgree(r, sr, "a0d", true);
            AssertIsMatchesAgree(r, sr, "a0", false);
            AssertIsMatchesAgree(r, sr, "a5def", true);
            AssertIsMatchesAgree(r, sr, "aa", false);
            AssertIsMatchesAgree(r, sr, "a3abcdefg", true);
            AssertIsMatchesAgree(r, sr, "a3abcdefgh", true);
            AssertIsMatchesAgree(r, sr, "a3abcdefghi", false);
        }

        [TestMethod]
        public void IsMatch2()
        {
            var pattern = @"^(abc|bbd|add|dde|ddd){1,2000}$";
            var r = new System.Text.RegularExpressions.Regex(pattern);
            var sr = new Regex(pattern);
            AssertIsMatchesAgree(r, sr, "addddd", true);
            AssertIsMatchesAgree(r, sr, "adddddd", false);
        }

        [TestMethod]
        public void IsMatch3()
        {
            var pattern = @".*(ab|ba)+$";
            var r = new System.Text.RegularExpressions.Regex(pattern, RegexOptions.Singleline);
            var sr = new Regex(pattern, RegexOptions.Singleline);
            AssertIsMatchesAgree(r, sr, "xxabbabbaba", true);
            AssertIsMatchesAgree(r, sr, "abba", true);
        }
        [TestMethod]
        public void IsMatch4()
        {
            var pattern = @"(ab|ba)+|ababbba";
            var r = new System.Text.RegularExpressions.Regex(pattern, RegexOptions.Singleline);
            var sr = new Regex(pattern, RegexOptions.Singleline);
            AssertIsMatchesAgree(r, sr, "ababba", true);
        }

        [TestMethod]
        public void IsMatch5()
        {
            var pattern = @"^(ab*a|bbba*)$";
            var r = new System.Text.RegularExpressions.Regex(pattern, RegexOptions.Singleline);
            var sr = new Regex(pattern, RegexOptions.Singleline);
            AssertIsMatchesAgree(r, sr, "aa", true);
            AssertIsMatchesAgree(r, sr, "abbbbbbbbbba", true);
            AssertIsMatchesAgree(r, sr, "bbb", true);
            AssertIsMatchesAgree(r, sr, "bbbaaaaaaaaa", true);
            AssertIsMatchesAgree(r, sr, "baba", false);
            AssertIsMatchesAgree(r, sr, "abab", false);
        }

        [TestMethod]
        public void IsMatch6()
        {
            var pattern = @"^.*(ab*a|bbba*)$";
            var r = new System.Text.RegularExpressions.Regex(pattern, RegexOptions.Singleline);
            var sr = new Regex(pattern, RegexOptions.Singleline);
            AssertIsMatchesAgree(r, sr, "xxxxaa", true);
            AssertIsMatchesAgree(r, sr, "xxabbbbbbbbbba", true);
            AssertIsMatchesAgree(r, sr, "xxbbb", true);
            AssertIsMatchesAgree(r, sr, "xxxbbbaaaaaaaaa", true);
            AssertIsMatchesAgree(r, sr, "babab", false);
            AssertIsMatchesAgree(r, sr, "ababx", false);
        }

        [TestMethod]
        public void IsMatch7()
        {
            var sr = new Regex(@"^abc[\0-\xFF]+$");

            // Create abc plus a random string
            byte[] bytes = RandomNumberGenerator.GetBytes(1000);
            char[] cs = Array.ConvertAll(bytes, b => (char)b);
            string s = new string(cs);
            var input = "abc" + s;

            Assert.IsTrue(sr.IsMatch(input));
            Assert.IsFalse(sr.IsMatch(input + "\uFFFD\uFFFD"));
        }

        [TestMethod]
        public void IsMatchLargeLoop()
        {
            var pattern = @"(ab|x|ba){1,20000}";
            var r = new System.Text.RegularExpressions.Regex(pattern);
            var sr = new Regex(pattern);
            AssertIsMatchesAgree(r, sr, "abba", true);
            AssertIsMatchesAgree(r, sr, "abxxx", true);
            AssertIsMatchesAgree(r, sr, "ab", true);
            AssertIsMatchesAgree(r, sr, "abxxxba", true);
            AssertIsMatchesAgree(r, sr, "baba", true);
            AssertIsMatchesAgree(r, sr, "abab", true);
            AssertIsMatchesAgree(r, sr, "aayybb", false);
        }

        [TestMethod]
        public void MatchesABC()
        {
            var sr = new Regex("abc", RegexOptions.IgnoreCase);
            var input = "xbxabcabxxxxaBCabcxx";
            Func<int, int, Match> m = (x, y) => new Match(x, y);
            var expectedMatches = new Match[]{ m(3, 3), m(12, 3), m(15, 3) };
            var matches = sr.Matches(input).ToList();
            CollectionAssert.AreEqual(expectedMatches, matches);
        }

        [TestMethod]
        public void MatchesSimpleLoops()
        {
            var sr = new Regex("bcd|(cc)+|e+");
            var input = "cccccbcdeeeee";
            Func<int, int, Match> m = (x, y) => new Match(x, y);
            var expectedMatches = new Match[]{ m(0, 4), m(5, 3), m(8, 5) };
            var matches = sr.Matches(input).ToList();
            CollectionAssert.AreEqual(expectedMatches, matches);
        }

        [TestMethod]
        public void MatchesBoundedLoop()
        {
            var sr = new Regex("a{2,4}");
            var input = "..aaaaaaaaaaa..";
            Func<int, int, Match> m = (x, y) => new Match(x, y);
            var expectedMatches = new Match[]{ m(2,4),m(6,4),m(10,3) };
            var matches = sr.Matches(input).ToList();
            Assert.AreEqual<int>(3, matches.Count);
            CollectionAssert.AreEqual(expectedMatches, matches);
        }

        [TestMethod]
        public void Vectorize()
        {
            var pattern = @"^\w\d\w{1,8}$";
            var r = new System.Text.RegularExpressions.Regex(pattern);
            var sr = new Regex(pattern, RegexOptions.Vectorize);
            AssertIsMatchesAgree(r, sr, "a0d", true);
            AssertIsMatchesAgree(r, sr, "a0", false);
            AssertIsMatchesAgree(r, sr, "a5def", true);
            AssertIsMatchesAgree(r, sr, "aa", false);
            AssertIsMatchesAgree(r, sr, "a3abcdefg", true);
            AssertIsMatchesAgree(r, sr, "a3abcdefgh", true);
            AssertIsMatchesAgree(r, sr, "a3abcdefghi", false);
        }

        static Match M(int index, int length) { return new Match(index, length); }

        [TestMethod]
        public void TestStartAnchor()
        {
            string pat = "^a{2,4}";
            var sr = new Regex(pat);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "aaaa\nab\naaa\nb\naabb";
            var sr_expectedMatches = new Match[] { M(0, 4) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestEndAnchor()
        {
            string pat = "abc$";
            var sr = new Regex(pat);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "abc\naabc\naabc";
            var sr_expectedMatches = new Match[] { M(10, 3) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestEndAnchor2()
        {
            string pat = "a*b*c$";
            var sr = new Regex(pat);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "abc\naabc\naabc";
            var sr_expectedMatches = new Match[] { M(9, 4) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestStartLineAnchor()
        {
            string pat = "^a{2,4}";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "aaaa\nab\naaa\nb\naabb";
            var sr_expectedMatches = new Match[] { M(0, 4), M(8, 3), M(14, 2) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestEndLineAnchor()
        {
            string pat = "ab+$";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "aaaa\nabbbc\nabbbb\ncccab\naabb";
            var sr_expectedMatches = new Match[] { M(11, 5), M(20, 2), M(24, 3) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestEndLineAnchor2()
        {
            string pat = "a*b+$";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "aaaa\nabbbc\nabbbb\ncccab\naabb";
            var sr_expectedMatches = new Match[] { M(11, 5), M(20, 2), M(23, 4) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestStartAndEndLineAnchors()
        {
            string pat = "^a*b+$";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "aaaa\nabbbc\nabbbb\ncccab\naabb";
            var sr_expectedMatches = new Match[] { M(11, 5), M(23, 4) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(2, r_matches.Count);
            Assert.AreEqual<int>(2, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestStartAndEndLineAnchors2()
        {
            string pat = "^a*b+$";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "aaab\nabbbc\nabbbb\ncccab\naabb";
            var sr_expectedMatches = new Match[] { M(0, 4), M(11, 5), M(23, 4) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestAllAnchors()
        {
            string pat = "\\Aabcd|abc\\z|^abc$";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "abcde\nabce\nabc\naabc\nab\nddabc";
            var sr_expectedMatches = new Match[] { M(0, 4), M(11,3), M(25,3)};
            var r_matches = r.Matches(input);
            var sr_matches = sr.Matches(input).ToList();
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestAllAnchorsSerializeDeserialize()
        {
            string pat = "\\Aabcd|abc\\z|^abc$";
            var sr1 = new Regex(pat, RegexOptions.Multiline);
            sr1.Serialize("test.txt");
            var sr = Regex.Deserialize("test.txt");
            var r = new System.Text.RegularExpressions.Regex(pat, System.Text.RegularExpressions.RegexOptions.Multiline);
            var input = "abcde\nabce\nabc\naabc\nab\nddabc";
            var sr_expectedMatches = new Match[] { M(0, 4), M(11, 3), M(25, 3) };
            var r_matches = r.Matches(input);
            var sr_matches = sr.Matches(input).ToList();
            Assert.AreEqual<int>(3, r_matches.Count);
            Assert.AreEqual<int>(3, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestRegressionCase()
        {
            string pat = @"[\r\n][a-zA-Z0-9/+\r\n]{44}";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "\nabcdefghijklmnopqrstuvwxyz0123456789/+ABCDEa";
            var sr_expectedMatches = new Match[] { M(0, 45) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }

        [TestMethod]
        public void TestRegressionCase2()
        {
            string pat = @"\n[a-zA-Z0-9/+\r\n]{44}";
            var sr = new Regex(pat, RegexOptions.Multiline);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "\nabcdefghijklmnopqrstuvwxyz0123456789/+ABCDEa";
            var sr_expectedMatches = new Match[] { M(0, 45) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }


        [TestMethod]
        public void TestRegressionCase3()
        {
            string pat = @"(a|ba)c";
            var sr = new Regex(pat);
            var r = new System.Text.RegularExpressions.Regex(pat);
            var input = "ac";
            var sr_expectedMatches = new Match[] { M(0, 2) };
            var sr_matches = sr.Matches(input).ToList();
            var r_matches = r.Matches(input);
            Assert.AreEqual<int>(1, r_matches.Count);
            Assert.AreEqual<int>(1, sr_matches.Count);
            CollectionAssert.AreEqual(sr_expectedMatches, sr_matches);
        }
    }
}