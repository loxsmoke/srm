using System;
using System.Collections.Generic;
using System.Text;

namespace System.Text.RegularExpressions
{
    public enum RegexNodeType
    {
        Oneloop = 3,    // a {,n}
        Notoneloop = 4, // .{,n}
        Setloop = 5,    // [\d]{,n}

        Onelazy = 6,    // a {,n}?
        Notonelazy = 7, // .{,n}?
        Setlazy = 8,    // [\d]{,n}?

        One = 9,        // a
        Notone = 10,    // [^a]
        Set = 11,       // [a-z\s]  \w \s \d

        Multi = 12,     // abcd
        Ref = 13,       // \#

        Bol = 14,       // ^
        Eol = 15,       // $
        Boundary = 16,  // \b
        Nonboundary = 17,   // \B
        Beginning = 18,     // \A
        Start = 19,     // \G
        EndZ = 20,      // \Z
        End = 21,       // \Z

        ECMABoundary = 41,
        NonECMABoundary = 42,

        Nothing = 22,   // []
        Empty = 23,     // ()

        Alternate = 24,     // a|b
        Concatenate = 25,   // ab

        Loop = 26,          // * + ? {,}
        Lazyloop = 27,      // *? +? ?? {,}?

        Capture = 28,       // ()
        Group = 29,         // (?:)
        Require = 30,       //  (?=) (?<=)
        Prevent = 31,       // (?!) (?<!)
        Greedy = 32,        // (?>) (?<)
        Testref = 33,       // (?(n) | )
        Testgroup = 34      // (?(...) | )
    }
}
