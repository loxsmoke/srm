//------------------------------------------------------------------------------
// <copyright file="RegexNode.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

// This RegexNode class is internal to the Regex package.
// It is built into a parsed tree for a regular expression.

// Implementation notes:
// 
// Since the node tree is a temporary data structure only used
// during compilation of the regexp to integer codes, it's
// designed for clarity and convenience rather than
// space efficiency.
//
// RegexNodes are built into a tree, linked by the _children list.
// Each node also has a _parent and _ichild member indicating
// its parent and which child # it is in its parent's list.
//
// RegexNodes come in as many types as there are constructs in
// a regular expression, for example, "concatenate", "alternate",
// "one", "rept", "group". There are also node types for basic
// peephole optimizations, e.g., "onerep", "notsetrep", etc.
//
// Because perl 5 allows "lookback" groups that scan backwards,
// each node also gets a "direction". Normally the value of
// boolean _backward = false.
//
// During parsing, top-level nodes are also stacked onto a parse
// stack (a stack of trees). For this purpose we have a _next
// pointer. [Note that to save a few bytes, we could overload the
// _parent pointer instead.]
//
// On the parse stack, each tree has a "role" - basically, the
// nonterminal in the grammar that the parser has currently
// assigned to the tree. That code is stored in _role.
//
// Finally, some of the different kinds of nodes have data.
// Two integers (for the looping constructs) are stored in
// _operands, an an object (either a string or a set)
// is stored in _data


namespace System.Text.RegularExpressions {

    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    public sealed class RegexNode 
    {
        /*
         * RegexNode types
         */

        // the following are leaves, and correspond to primitive operations

        //    static final int Onerep     = RegexCode.Onerep;     // c,n      a {n}
        //    static final int Notonerep  = RegexCode.Notonerep;  // c,n      .{n}
        //    static final int Setrep     = RegexCode.Setrep;     // set,n    \d {n}

        //internal const int Oneloop    = RegexCode.Oneloop;    // c,n      a*
        //internal const int Notoneloop = RegexCode.Notoneloop; // c,n      .*
        //internal const int Setloop    = RegexCode.Setloop;    // set,n    \d*
        //
        //internal const int Onelazy    = RegexCode.Onelazy;    // c,n      a*?
        //internal const int Notonelazy = RegexCode.Notonelazy; // c,n      .*?
        //internal const int Setlazy    = RegexCode.Setlazy;    // set,n    \d*?
        //
        //internal const int One        = RegexCode.One;        // char     a
        //internal const int Notone     = RegexCode.Notone;     // char     . [^a]
        //internal const int Set        = RegexCode.Set;        // set      [a-z] \w \s \d
        //
        //internal const int Multi      = RegexCode.Multi;      // string   abcdef
        //internal const int Ref        = RegexCode.Ref;        // index    \1
        //
        //internal const int Bol        = RegexCode.Bol;        //          ^
        //internal const int Eol        = RegexCode.Eol;        //          $
        //internal const int Boundary   = RegexCode.Boundary;   //          \b
        //internal const int Nonboundary= RegexCode.Nonboundary;//          \B
        //internal const int ECMABoundary   = RegexCode.ECMABoundary;    // \b
        //internal const int NonECMABoundary= RegexCode.NonECMABoundary; // \B
        //internal const int Beginning  = RegexCode.Beginning;  //          \A
        //internal const int Start      = RegexCode.Start;      //          \G
        //internal const int EndZ       = RegexCode.EndZ;       //          \Z
        //internal const int End        = RegexCode.End;        //          \z

        // (note: End               = 21;)

        // interior nodes do not correpond to primitive operations, but
        // control structures compositing other operations

        // concat and alternate take n children, and can run forward or backwards

        //internal const int Nothing    = 22;                   //          []
        //internal const int Empty      = 23;                   //          ()

        //internal const int Alternate  = 24;                   //          a|b
        //internal const int Concatenate= 25;                   //          ab
        //
        //internal const int Loop       = 26;                   // m,x      * + ? {,}
        //internal const int Lazyloop   = 27;                   // m,x      *? +? ?? {,}?
        //
        //internal const int Capture    = 28;                   // n        ()
        //internal const int Group      = 29;                   //          (?:)
        //internal const int Require    = 30;                   //          (?=) (?<=)
        //internal const int Prevent    = 31;                   //          (?!) (?<!)
        //internal const int Greedy     = 32;                   //          (?>) (?<)
        //internal const int Testref    = 33;                   //          (?(n) | )
        //internal const int Testgroup  = 34;                   //          (?(...) | )

        /*
         * RegexNode data members
         * 
         */

        public RegexNodeType Type;

        public List<RegexNode> children;

        public RegexCharClass DecodedSet
        { 
            get 
            {
                if (Type == RegexNodeType.Setloop ||
                    Type == RegexNodeType.Setlazy ||
                    Type == RegexNodeType.Set)
                {
                    return RegexCharClass.Parse(_str);
                    // return RegexCharClass.SetDescription(_str);
                }
                return null;
            } 
        }
        public string         _str;
        public char           oneChar;
        public int            minIterations;
        public int            maxIterations;
        public RegexOptions   options;

        public RegexNode   nextNode;
        public int ChildCount => children == null ? 0 : children.Count;
        public bool RightToLeft => (options & RegexOptions.RightToLeft) != 0;

        public RegexNode(RegexNodeType type, RegexOptions options)
        {
            Type = type;
            this.options = options;
        }

        public RegexNode(RegexNodeType type, RegexOptions options, char ch)
        {
            Type = type;
            this.options = options;
            oneChar = ch;
        }

        public RegexNode(RegexNodeType type, RegexOptions options, string str)
        {
            Type = type;
            this.options = options;
            _str = str;
        }

        public RegexNode(RegexNodeType type, RegexOptions options, int min)
        {
            Type = type;
            this.options = options;
            minIterations = min;
        }

        public RegexNode(RegexNodeType type, RegexOptions options, int min, int max)
        {
            Type = type;
            this.options = options;
            minIterations = min;
            maxIterations = max;
        }


        public RegexNode ReverseLeft()
        {
            if (RightToLeft && Type == RegexNodeType.Concatenate && children != null)
            {
                children.Reverse(0, children.Count);
            }

            return this;
        }

        /// <summary>
        /// Removes redundant nodes from the subtree, and returns a reduced subtree.
        /// </summary>
        /// <returns></returns>
        public RegexNode Reduce()
        {
            switch (Type)
            {
                case RegexNodeType.Alternate:
                    return ReduceAlternation();

                case RegexNodeType.Concatenate:
                    return ReduceConcatenation();

                case RegexNodeType.Loop:
                case RegexNodeType.Lazyloop:
                    return ReduceRep();

                case RegexNodeType.Group:
                    return ReduceGroup();

                case RegexNodeType.Set:
                case RegexNodeType.Setloop:
                    return ReduceSet();

                default:
                    return this;
            }
        }


        /*
         * StripEnation:
         *
         * Simple optimization. If a concatenation or alternation has only
         * one child strip out the intermediate node. If it has zero children,
         * turn it into an empty.
         * 
         */

        public RegexNode StripEnation(RegexNodeType emptyType)
        {
            switch (ChildCount)
            {
                case 0:
                    return new RegexNode(emptyType, options);
                case 1:
                    return Child(0);
                default:
                    return this;
            }
        }

        /*
         * ReduceGroup:
         *
         * Simple optimization. Once parsed into a tree, noncapturing groups
         * serve no function, so strip them out.
         */

        public RegexNode ReduceGroup()
        {
            RegexNode u;

            for (u = this; u.Type == RegexNodeType.Group; )
                u = u.Child(0);

            return u;
        }

        /*
         * ReduceRep:
         *
         * Nested repeaters just get multiplied with each other if they're not
         * too lumpy
         */

        public RegexNode ReduceRep()
        {
            RegexNode u;
            RegexNode child;
            RegexNodeType type;

            u = this;
            type = Type;
            var min = minIterations;
            var max = maxIterations;

            for (; ; )
            {
                if (u.ChildCount == 0)
                    break;

                child = u.Child(0);

                // multiply reps of the same type only
                if (child.Type != type) 
                {
                    RegexNodeType childType = child.Type;

                    if (!(childType >= RegexNodeType.Oneloop && 
                        childType <= RegexNodeType.Setloop && 
                        type == RegexNodeType.Loop ||
                          childType >= RegexNodeType.Onelazy && 
                          childType <= RegexNodeType.Setlazy && 
                          type == RegexNodeType.Lazyloop))
                        break;
                }

                // child can be too lumpy to blur, e.g., (a {100,105}) {3} or (a {2,})?
                // [but things like (a {2,})+ are not too lumpy...]
                if (u.minIterations == 0 && child.minIterations > 1 || child.maxIterations < child.minIterations * 2)
                    break;

                u = child;
                if (u.minIterations > 0)
                    u.minIterations = min = ((int.MaxValue - 1) / u.minIterations < min) ? int.MaxValue : u.minIterations * min;
                if (u.maxIterations > 0)
                    u.maxIterations = max = ((int.MaxValue - 1) / u.maxIterations < max) ? int.MaxValue : u.maxIterations * max;
            }

            return min == int.MaxValue ? new RegexNode(RegexNodeType.Nothing, options) : u;
        }

        /*
         * ReduceSet:
         *
         * Simple optimization. If a set is a singleton, an inverse singleton,
         * or empty, it's transformed accordingly.
         */

        public RegexNode ReduceSet()
        {
            // Extract empty-set, one and not-one case as special

            if (RegexCharClass.IsEmpty(_str))
            {
                Type = RegexNodeType.Nothing;
                _str = null;
            }
            else if (RegexCharClass.IsSingleton(_str))
            {
                oneChar = RegexCharClass.SingletonChar(_str);
                _str = null;
                Type += (RegexNodeType.One - RegexNodeType.Set); // -2
            }
            else if (RegexCharClass.IsSingletonInverse(_str))
            {
                oneChar = RegexCharClass.SingletonChar(_str);
                _str = null;
                Type += (RegexNodeType.Notone - RegexNodeType.Set); // -1
            }

            return this;
        }

        /*
         * ReduceAlternation:
         *
         * Basic optimization. Single-letter alternations can be replaced
         * by faster set specifications, and nested alternations with no
         * intervening operators can be flattened:
         *
         * a|b|c|def|g|h -> [a-c]|def|[gh]
         * apple|(?:orange|pear)|grape -> apple|orange|pear|grape
         *
         * <CONSIDER>common prefix reductions such as winner|windows -> win(?:ner|dows)</CONSIDER>
         */

        public RegexNode ReduceAlternation()
        {
            // Combine adjacent sets/chars

            bool wasLastSet;
            bool lastNodeCannotMerge;
            RegexOptions optionsLast;
            RegexOptions optionsAt;
            int i;
            int j;
            RegexNode at;
            RegexNode prev;

            if (children == null)
                return new RegexNode(RegexNodeType.Nothing, options);

            wasLastSet = false;
            lastNodeCannotMerge = false;
            optionsLast = 0;

            for (i = 0, j = 0; i < children.Count; i++, j++)
            {
                at = children[i];

                if (j < i)
                    children[j] = at;

                for (; ; )
                {
                    if (at.Type == RegexNodeType.Alternate)
                    {
                        for (int k = 0; k < at.children.Count; k++)
                            at.children[k].nextNode = this;

                        children.InsertRange(i + 1, at.children);
                        j--;
                    }
                    else if (at.Type == RegexNodeType.Set || at.Type == RegexNodeType.One)
                    {
                        // Cannot merge sets if L or I options differ, or if either are negated.
                        optionsAt = at.options & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase);


                        if (at.Type == RegexNodeType.Set)
                        {
                            if (!wasLastSet || optionsLast != optionsAt || lastNodeCannotMerge || !RegexCharClass.IsMergeable(at._str))
                            {
                                wasLastSet = true;
                                lastNodeCannotMerge = !RegexCharClass.IsMergeable(at._str);
                                optionsLast = optionsAt;
                                break;
                            }
                        }
                        else if (!wasLastSet || optionsLast != optionsAt || lastNodeCannotMerge)
                        {
                            wasLastSet = true;
                            lastNodeCannotMerge = false;
                            optionsLast = optionsAt;
                            break;
                        }

                        
                        // The last node was a Set or a One, we're a Set or One and our options are the same.
                        // Merge the two nodes.
                        j--;
                        prev = children[j];
                        
                        RegexCharClass prevCharClass;
                        if (prev.Type == RegexNodeType.One) 
                        {
                            prevCharClass = new RegexCharClass();
                            prevCharClass.AddChar(prev.oneChar);
                        }
                        else 
                        {
                            prevCharClass = RegexCharClass.Parse(prev._str);
                        }
                        
                        if (at.Type == RegexNodeType.One) 
                        {
                            prevCharClass.AddChar(at.oneChar);
                        }
                        else 
                        {
                            RegexCharClass atCharClass = RegexCharClass.Parse(at._str);
                            prevCharClass.AddCharClass(atCharClass);
                        }
                        
                        prev.Type = RegexNodeType.Set;
                        prev._str  = prevCharClass.ToStringClass();
                        
                    }
                    else if (at.Type == RegexNodeType.Nothing) 
                    {
                        j--;
                    }
                    else 
                    {
                        wasLastSet = false;
                        lastNodeCannotMerge = false;
                    }
                    break;
                }
            }

            if (j < i)
                children.RemoveRange(j, i - j);

            return StripEnation(RegexNodeType.Nothing);
        }

        /*
         * ReduceConcatenation:
         *
         * Basic optimization. Adjacent strings can be concatenated.
         *
         * (?:abc)(?:def) -> abcdef
         */

        public RegexNode ReduceConcatenation()
        {
            // Eliminate empties and concat adjacent strings/chars

            bool wasLastString;
            RegexOptions optionsLast;
            RegexOptions optionsAt;
            int i;
            int j;

            if (children == null)
                return new RegexNode(RegexNodeType.Empty, options);

            wasLastString = false;
            optionsLast = 0;

            for (i = 0, j = 0; i < children.Count; i++, j++) {
                RegexNode at;
                RegexNode prev;

                at = children[i];

                if (j < i)
                    children[j] = at;

                if (at.Type == RegexNodeType.Concatenate &&
                    ((at.options & RegexOptions.RightToLeft) == (options & RegexOptions.RightToLeft))) 
                {
                    for (int k = 0; k < at.children.Count; k++)
                        at.children[k].nextNode = this;

                    children.InsertRange(i + 1, at.children);
                    j--;
                }
                else if (at.Type == RegexNodeType.Multi ||
                         at.Type == RegexNodeType.One) 
                {
                    // Cannot merge strings if L or I options differ
                    optionsAt = at.options & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase);

                    if (!wasLastString || optionsLast != optionsAt) {
                        wasLastString = true;
                        optionsLast = optionsAt;
                        continue;
                    }

                    prev = children[--j];

                    if (prev.Type == RegexNodeType.One) 
                    {
                        prev.Type = RegexNodeType.Multi;
                        prev._str = Convert.ToString(prev.oneChar, CultureInfo.InvariantCulture);
                    }

                    if ((optionsAt & RegexOptions.RightToLeft) == 0) 
                    {
                        if (at.Type == RegexNodeType.One)
                            prev._str += at.oneChar.ToString();
                        else
                            prev._str += at._str;
                    }
                    else 
                    {
                        if (at.Type == RegexNodeType.One)
                            prev._str = at.oneChar.ToString() + prev._str;
                        else
                            prev._str = at._str + prev._str;
                    }

                }
                else if (at.Type == RegexNodeType.Empty) 
                {
                    j--;
                }
                else 
                {
                    wasLastString = false;
                }
            }

            if (j < i)
                children.RemoveRange(j, i - j);

            return StripEnation(RegexNodeType.Empty);
        }

        public RegexNode MakeQuantifier(bool lazy, int min, int max)
        {
            if (min == 0 && max == 0)
                return new RegexNode(RegexNodeType.Empty, options);

            if (min == 1 && max == 1)
                return this;

            switch (Type) 
            {
                case RegexNodeType.One:
                    Type = lazy ? RegexNodeType.Onelazy : RegexNodeType.Oneloop;
                    minIterations = min;
                    maxIterations = max;
                    return this;

                case RegexNodeType.Notone:
                    Type = lazy ? RegexNodeType.Notonelazy : RegexNodeType.Notoneloop;
                    minIterations = min;
                    maxIterations = max;
                    return this;

                case RegexNodeType.Set:
                    Type = lazy ? RegexNodeType.Setlazy : RegexNodeType.Setloop;
                    minIterations = min;
                    maxIterations = max;
                    return this;

                default:
                    {
                        var result = new RegexNode(lazy ? RegexNodeType.Lazyloop : RegexNodeType.Loop, options, min, max);
                        result.AddChild(this);
                        return result;
                    }
            }
        }

        public void AddChild(RegexNode newChild) 
        {
            if (children == null)
                children = new List<RegexNode>(4);

            var reducedChild = newChild.Reduce();

            children.Add(reducedChild);
            reducedChild.nextNode = this;
        }

        public RegexNode Child(int i) 
        {
            return children[i];
        }

        public override string ToString()
        {
            StringBuilder ArgSb = new StringBuilder();

            ArgSb.Append(Type.ToString());

            if ((options & RegexOptions.ExplicitCapture) != 0)
                ArgSb.Append("-C");
            if ((options & RegexOptions.IgnoreCase) != 0)
                ArgSb.Append("-I");
            if ((options & RegexOptions.RightToLeft) != 0)
                ArgSb.Append("-L");
            if ((options & RegexOptions.Multiline) != 0)
                ArgSb.Append("-M");
            if ((options & RegexOptions.Singleline) != 0)
                ArgSb.Append("-S");
            if ((options & RegexOptions.IgnorePatternWhitespace) != 0)
                ArgSb.Append("-X");
            if ((options & RegexOptions.ECMAScript) != 0)
                ArgSb.Append("-E");

            switch (Type)
            {
                case RegexNodeType.Oneloop:
                case RegexNodeType.Notoneloop:
                case RegexNodeType.Onelazy:
                case RegexNodeType.Notonelazy:
                case RegexNodeType.One:
                case RegexNodeType.Notone:
                    ArgSb.Append("(Ch = " + RegexCharClass.CharDescription(oneChar) + ")");
                    break;
                case RegexNodeType.Capture:
                    ArgSb.Append($"(index = {minIterations}, unindex = {maxIterations})");
                    break;
                case RegexNodeType.Ref:
                case RegexNodeType.Testref:
                    ArgSb.Append($"(index = {minIterations})");
                    break;
                case RegexNodeType.Multi:
                    ArgSb.Append($"(String = {_str})");
                    break;
                case RegexNodeType.Set:
                case RegexNodeType.Setloop:
                case RegexNodeType.Setlazy:
                    ArgSb.Append("(Set = " + RegexCharClass.SetDescription(_str) + ")");
                    break;
            }

            switch (Type)
            {
                case RegexNodeType.Oneloop:
                case RegexNodeType.Notoneloop:
                case RegexNodeType.Onelazy:
                case RegexNodeType.Notonelazy:
                case RegexNodeType.Setloop:
                case RegexNodeType.Setlazy:
                case RegexNodeType.Loop:
                case RegexNodeType.Lazyloop:
                    ArgSb.Append("(Min = " + minIterations.ToString(CultureInfo.InvariantCulture) + ", Max = " + (maxIterations == Int32.MaxValue ? "inf" : Convert.ToString(maxIterations, CultureInfo.InvariantCulture)) + ")");
                    break;
            }

            return ArgSb.ToString();
        }
#if DBG
        internal static String[] TypeStr = new String[] {
            "Onerep", "Notonerep", "Setrep",
            "Oneloop", "Notoneloop", "Setloop",
            "Onelazy", "Notonelazy", "Setlazy",
            "One", "Notone", "Set",
            "Multi", "Ref",
            "Bol", "Eol", "Boundary", "Nonboundary",
            "ECMABoundary", "NonECMABoundary",
            "Beginning", "Start", "EndZ", "End",
            "Nothing", "Empty",
            "Alternate", "Concatenate",
            "Loop", "Lazyloop",
            "Capture", "Group", "Require", "Prevent", "Greedy",
            "Testref", "Testgroup"};

        internal String Description() {

            StringBuilder ArgSb = new StringBuilder();

            ArgSb.Append(TypeStr[_type]);

            if ((_options & RegexOptions.ExplicitCapture) != 0)
                ArgSb.Append("-C");
            if ((_options & RegexOptions.IgnoreCase) != 0)
                ArgSb.Append("-I");
            if ((_options & RegexOptions.RightToLeft) != 0)
                ArgSb.Append("-L");
            if ((_options & RegexOptions.Multiline) != 0)
                ArgSb.Append("-M");
            if ((_options & RegexOptions.Singleline) != 0)
                ArgSb.Append("-S");
            if ((_options & RegexOptions.IgnorePatternWhitespace) != 0)
                ArgSb.Append("-X");
            if ((_options & RegexOptions.ECMAScript) != 0)
                ArgSb.Append("-E");

            switch (_type) {
                case Oneloop:
                case Notoneloop:
                case Onelazy:
                case Notonelazy:
                case One:
                case Notone:
                    ArgSb.Append("(Ch = " + RegexCharClass.CharDescription(_ch) + ")");
                    break;
                case Capture:
                    ArgSb.Append("(index = " + _m.ToString(CultureInfo.InvariantCulture) + ", unindex = " + _n.ToString(CultureInfo.InvariantCulture) + ")");
                    break;
                case Ref:
                case Testref:
                    ArgSb.Append("(index = " + _m.ToString(CultureInfo.InvariantCulture) + ")");
                    break;
                case Multi:
                    ArgSb.Append("(String = " + _str + ")");
                    break;
                case Set:
                case Setloop:
                case Setlazy:
                    ArgSb.Append("(Set = " + RegexCharClass.SetDescription(_str) + ")");
                    break;
            }

            switch (_type) {
                case Oneloop:
                case Notoneloop:
                case Onelazy:
                case Notonelazy:
                case Setloop:
                case Setlazy:
                case Loop:
                case Lazyloop:
                    ArgSb.Append("(Min = " + _m.ToString(CultureInfo.InvariantCulture) + ", Max = " + (_n == Int32.MaxValue ? "inf" : Convert.ToString(_n, CultureInfo.InvariantCulture)) + ")");
		    break;
            }

            return ArgSb.ToString();
        }

        internal const String Space = "                                ";

        internal void Dump() {
            List<int> Stack = new List<int>();
            RegexNode CurNode;
            int CurChild;

            CurNode = this;
            CurChild = 0;

            Debug.WriteLine(CurNode.Description());

            for (;;) {
                if (CurNode._children != null && CurChild < CurNode._children.Count) {
                    Stack.Add(CurChild + 1);
                    CurNode = CurNode._children[CurChild];
                    CurChild = 0;

                    int Depth = Stack.Count;
                    if (Depth > 32)
                        Depth = 32;

                    Debug.WriteLine(Space.Substring(0, Depth) + CurNode.Description());
                }
                else {
                    if (Stack.Count == 0)
                        break;

                    CurChild = Stack[Stack.Count - 1];
                    Stack.RemoveAt(Stack.Count - 1);
                    CurNode = CurNode._next;
                }
            }
        }
#endif

    }

}
