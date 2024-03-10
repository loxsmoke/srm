using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.SRM.Tests
{
    [TestClass]
    public class PartialMatchingTests
    {
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
