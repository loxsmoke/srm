//------------------------------------------------------------------------------
// <copyright file="RegexFCD.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

// This RegexFCD class is internal to the Regex package.
// It builds a bunch of FC information (RegexFC) about
// the regex for optimization purposes.

// Implementation notes:
// 
// This step is as simple as walking the tree and emitting
// sequences of codes.

namespace System.Text.RegularExpressions {

    using System.Collections;
    using System.Globalization;
    
    internal sealed class RegexFCD 
    {
        private int[]      _intStack;
        private int        _intDepth;    
        private RegexFC[]  _fcStack;
        private int        _fcDepth;
        private bool    _skipAllChildren;      // don't process any more children at the current level
        private bool    _skipchild;            // don't process the current child. 
        private bool    _failed = false;
        
        private const int BeforeChild = 64;
        private const int AfterChild = 128;

        // where the regex can be pegged

        internal  const int Beginning  = 0x0001;
        internal  const int Bol        = 0x0002;
        internal  const int Start      = 0x0004;
        internal  const int Eol        = 0x0008;
        internal  const int EndZ       = 0x0010;
        internal  const int End        = 0x0020;
        internal  const int Boundary   = 0x0040;
        internal  const int ECMABoundary = 0x0080;

        /*
         * This is the one of the only two functions that should be called from outside.
         * It takes a RegexTree and computes the set of chars that can start it.
         */
        internal static RegexPrefix FirstChars(RegexTree t) {
            RegexFCD s = new RegexFCD();
            RegexFC fc = s.RegexFCFromRegexTree(t);

            if (fc == null || fc._nullable)
                return null;
            
            CultureInfo culture = ((t.options & RegexOptions.CultureInvariant) != 0) ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
            return new RegexPrefix(fc.GetFirstChars(culture), fc.IsCaseInsensitive());
        }

        /*
         * This is a related computation: it takes a RegexTree and computes the
         * leading substring if it see one. It's quite trivial and gives up easily.
         */
        internal static RegexPrefix Prefix(RegexTree tree) 
        {
            RegexNode curNode;
            RegexNode concatNode = null;
            int nextChild = 0;

            curNode = tree.root;

            for (;;) 
            {
                switch (curNode.Type) 
                {
                    case RegexNodeType.Concatenate:
                        if (curNode.ChildCount > 0) 
                        {
                            concatNode = curNode;
                            nextChild = 0;
                        }
                        break;

                    case RegexNodeType.Greedy:
                    case RegexNodeType.Capture:
                        curNode = curNode.Child(0);
                        concatNode = null;
                        continue;

                    case RegexNodeType.Oneloop:
                    case RegexNodeType.Onelazy:
                        if (curNode.minIterations > 0) {
                            string pref = String.Empty.PadRight(curNode.minIterations, curNode.oneChar);
                            return new RegexPrefix(pref, 0 != (curNode.options & RegexOptions.IgnoreCase));
                        }
                        else
                            return RegexPrefix.Empty;
                        
                    case RegexNodeType.One:
                        return new RegexPrefix(curNode.oneChar.ToString(CultureInfo.InvariantCulture), 0 != (curNode.options & RegexOptions.IgnoreCase));
                            
                    case RegexNodeType.Multi:
                        return new RegexPrefix(curNode._str, 0 != (curNode.options & RegexOptions.IgnoreCase));

                    case RegexNodeType.Bol:
                    case RegexNodeType.Eol:
                    case RegexNodeType.Boundary:
                    case RegexNodeType.ECMABoundary:
                    case RegexNodeType.Beginning:
                    case RegexNodeType.Start:
                    case RegexNodeType.EndZ:
                    case RegexNodeType.End:
                    case RegexNodeType.Empty:
                    case RegexNodeType.Require:
                    case RegexNodeType.Prevent:
                        break;

                    default:
                        return RegexPrefix.Empty;
                }

                if (concatNode == null || nextChild >= concatNode.ChildCount)
                    return RegexPrefix.Empty;

                curNode = concatNode.Child(nextChild++);
            }
        }

        /*
         * Yet another related computation: it takes a RegexTree and computes the
         * leading anchors that it encounters.
         */
        internal static int Anchors(RegexTree tree) 
        {
            RegexNode curNode;
            RegexNode concatNode = null;
            int nextChild = 0;
            int result = 0;

            curNode = tree.root;

            for (;;) 
            {
                switch (curNode.Type) 
                {
                    case RegexNodeType.Concatenate:
                        if (curNode.ChildCount > 0) 
                        {
                            concatNode = curNode;
                            nextChild = 0;
                        }
                        break;

                    case RegexNodeType.Greedy:
                    case RegexNodeType.Capture:
                        curNode = curNode.Child(0);
                        concatNode = null;
                        continue;

                    case RegexNodeType.Bol:
                    case RegexNodeType.Eol:
                    case RegexNodeType.Boundary:
                    case RegexNodeType.ECMABoundary:
                    case RegexNodeType.Beginning:
                    case RegexNodeType.Start:
                    case RegexNodeType.EndZ:
                    case RegexNodeType.End:
                        return result | AnchorFromType(curNode.Type);

                    case RegexNodeType.Empty:
                    case RegexNodeType.Require:
                    case RegexNodeType.Prevent:
                        break;

                    default:
                        return result;
                }

                if (concatNode == null || nextChild >= concatNode.ChildCount)
                    return result;

                curNode = concatNode.Child(nextChild++);
            }
        }

        /*
         * Convert anchor type to anchor bit.
         */
        private static int AnchorFromType(RegexNodeType type) 
        {
            switch (type) 
            {
                case RegexNodeType.Bol:             return Bol;         
                case RegexNodeType.Eol:             return Eol;         
                case RegexNodeType.Boundary:        return Boundary;    
                case RegexNodeType.ECMABoundary:    return ECMABoundary;
                case RegexNodeType.Beginning:       return Beginning;   
                case RegexNodeType.Start:           return Start;       
                case RegexNodeType.EndZ:            return EndZ;        
                case RegexNodeType.End:             return End;         
                default:                        return 0;
            }
        }

#if DBG
        internal static String AnchorDescription(int anchors) {
            StringBuilder sb = new StringBuilder();

            if (0 != (anchors & Beginning))     sb.Append(", Beginning");
            if (0 != (anchors & Start))         sb.Append(", Start");
            if (0 != (anchors & Bol))           sb.Append(", Bol");
            if (0 != (anchors & Boundary))      sb.Append(", Boundary");
            if (0 != (anchors & ECMABoundary))  sb.Append(", ECMABoundary");
            if (0 != (anchors & Eol))           sb.Append(", Eol");
            if (0 != (anchors & End))           sb.Append(", End");
            if (0 != (anchors & EndZ))          sb.Append(", EndZ");

            if (sb.Length >= 2)
                return(sb.ToString(2, sb.Length - 2));

            return "None";
        }
#endif

        /*
         * private constructor; can't be created outside
         */
        private RegexFCD() {
            _fcStack = new RegexFC[32];
            _intStack = new int[32];
        }

        /*
         * To avoid recursion, we use a simple integer stack.
         * This is the push.
         */
        private void PushInt(int I) {
            if (_intDepth >= _intStack.Length) {
                int [] expanded = new int[_intDepth * 2];

                System.Array.Copy(_intStack, 0, expanded, 0, _intDepth);

                _intStack = expanded;
            }

            _intStack[_intDepth++] = I;
        }

        /*
         * True if the stack is empty.
         */
        private bool IntIsEmpty() {
            return _intDepth == 0;
        }

        /*
         * This is the pop.
         */
        private int PopInt() {
            return _intStack[--_intDepth];
        }

        /*
          * We also use a stack of RegexFC objects.
          * This is the push.
          */
        private void PushFC(RegexFC fc) {
            if (_fcDepth >= _fcStack.Length) {
                RegexFC[] expanded = new RegexFC[_fcDepth * 2];

                System.Array.Copy(_fcStack, 0, expanded, 0, _fcDepth);
                _fcStack = expanded;
            }

            _fcStack[_fcDepth++] = fc;
        }

        /*
         * True if the stack is empty.
         */
        private bool FCIsEmpty() {
            return _fcDepth == 0;
        }

        /*
         * This is the pop.
         */
        private RegexFC PopFC() {
            return _fcStack[--_fcDepth];
        }

        /*
         * This is the top.
         */
        private RegexFC TopFC() {
            return _fcStack[_fcDepth - 1];
        }

        /*
         * The main FC computation. It does a shortcutted depth-first walk
         * through the tree and calls CalculateFC to emits code before
         * and after each child of an interior node, and at each leaf.
         */
        private RegexFC RegexFCFromRegexTree(RegexTree tree) 
        {
            RegexNode curNode;
            int curChild;

            curNode = tree.root;
            curChild = 0;

            for (;;) 
            {
                if (curNode.children == null) 
                {
                    // This is a leaf node
                    CalculateFC(curNode.Type, curNode, 0);
                }
                else if (curChild < curNode.children.Count && !_skipAllChildren) 
                {
                    // This is an interior node, and we have more children to analyze
                    CalculateFCBeforeChild(curNode.Type, curNode, curChild);

                    if (!_skipchild) {
                        curNode = (RegexNode)curNode.children[curChild];
                        // this stack is how we get a depth first walk of the tree. 
                        PushInt(curChild);
                        curChild = 0;
                    }
                    else {
                        curChild++;
                        _skipchild = false;
                    }
                    continue;
                }
                
                // This is an interior node where we've finished analyzing all the children, or
                // the end of a leaf node. 
                _skipAllChildren = false;

                if (IntIsEmpty())
                    break;

                curChild = PopInt();
                curNode = curNode.nextNode;

                CalculateFCAfterChild(curNode.Type, curNode, curChild);
                if (_failed)
                    return null;
                
                curChild++;
            }

            if (FCIsEmpty())
                return null; 

            return PopFC();
        }

        /*
         * Called in Beforechild to prevent further processing of the current child
         */
        private void SkipChild() 
        {
            _skipchild = true;
        }

        private void CalculateFCBeforeChild(RegexNodeType NodeType, RegexNode node, int CurIndex) =>
            CalculateFC(NodeType, node, CurIndex, true);

        private void CalculateFCAfterChild(RegexNodeType NodeType, RegexNode node, int CurIndex) =>
            CalculateFC(NodeType, node, CurIndex, false, true);

        /*
         * FC computation and shortcut cases for each node type
         */
        private void CalculateFC(RegexNodeType NodeType, RegexNode node, int CurIndex, bool beforeChild = false, bool afterChild = false)
        {
            bool ci = false;
            bool rtl = false;

            if (NodeType <= RegexNodeType.Ref) 
            {
                if ((node.options & RegexOptions.IgnoreCase) != 0)
                    ci = true;
                if ((node.options & RegexOptions.RightToLeft) != 0)
                    rtl = true;
            }

            if (beforeChild)
            {
                switch (NodeType)
                {
                    case RegexNodeType.Concatenate:
                    case RegexNodeType.Alternate:
                    case RegexNodeType.Testref:
                    case RegexNodeType.Loop:
                    case RegexNodeType.Lazyloop:

                    case RegexNodeType.Group:
                    case RegexNodeType.Capture:
                    case RegexNodeType.Greedy:
                        return;

                    case RegexNodeType.Require:
                    case RegexNodeType.Prevent:
                        SkipChild();
                        PushFC(new RegexFC(true));
                        return;

                    case RegexNodeType.Testgroup:
                        if (CurIndex == 0)
                            SkipChild();
                        return;
                }
                throw new ArgumentException(SR.GetString(SR.UnexpectedOpcode, NodeType.ToString()));
            }

            if (afterChild)
            {
                switch (NodeType)
                {
                    case RegexNodeType.Concatenate:
                        if (CurIndex != 0)
                        {
                            RegexFC child = PopFC();
                            RegexFC cumul = TopFC();

                            _failed = !cumul.AddFC(child, true);
                        }

                        if (!TopFC()._nullable)
                            _skipAllChildren = true;
                        return;

                    case RegexNodeType.Testgroup:
                        if (CurIndex > 1)
                        {
                            RegexFC child = PopFC();
                            RegexFC cumul = TopFC();

                            _failed = !cumul.AddFC(child, false);
                        }
                        return;

                    case RegexNodeType.Alternate:
                    case RegexNodeType.Testref:
                        if (CurIndex != 0)
                        {
                            RegexFC child = PopFC();
                            RegexFC cumul = TopFC();

                            _failed = !cumul.AddFC(child, false);
                        }
                        return;

                    case RegexNodeType.Loop:
                    case RegexNodeType.Lazyloop:
                        if (node.minIterations == 0)
                            TopFC()._nullable = true;
                        return;

                    case RegexNodeType.Group:
                    case RegexNodeType.Capture:
                    case RegexNodeType.Greedy:
                    case RegexNodeType.Require:
                    case RegexNodeType.Prevent:
                        return;
                }
                throw new ArgumentException(SR.GetString(SR.UnexpectedOpcode, NodeType.ToString()));
            }

            switch (NodeType) 
            {
                case RegexNodeType.Empty:
                    PushFC(new RegexFC(true));
                    break;

                case RegexNodeType.One:
                case RegexNodeType.Notone:
                    PushFC(new RegexFC(node.oneChar, NodeType == RegexNodeType.Notone, false, ci));
                    break;

                case RegexNodeType.Oneloop:
                case RegexNodeType.Onelazy:
                    PushFC(new RegexFC(node.oneChar, false, node.minIterations == 0, ci));
                    break;

                case RegexNodeType.Notoneloop:
                case RegexNodeType.Notonelazy:
                    PushFC(new RegexFC(node.oneChar, true, node.minIterations == 0, ci));
                    break;

                case RegexNodeType.Multi:
                    if (node._str.Length == 0)
                        PushFC(new RegexFC(true));
                    else if (!rtl)
                        PushFC(new RegexFC(node._str[0], false, false, ci));
                    else
                        PushFC(new RegexFC(node._str[node._str.Length - 1], false, false, ci));
                    break;

                case RegexNodeType.Set:
                    PushFC(new RegexFC(node._str, false, ci));
                    break;

                case RegexNodeType.Setloop:
                case RegexNodeType.Setlazy:
                    PushFC(new RegexFC(node._str, node.minIterations == 0, ci));
                    break;

                case RegexNodeType.Ref:
                    PushFC(new RegexFC(RegexCharClass.AnyClass, true, false));
                    break;

                case RegexNodeType.Nothing:
                case RegexNodeType.Bol:
                case RegexNodeType.Eol:
                case RegexNodeType.Boundary:
                case RegexNodeType.Nonboundary:
                case RegexNodeType.ECMABoundary:
                case RegexNodeType.NonECMABoundary:
                case RegexNodeType.Beginning:
                case RegexNodeType.Start:
                case RegexNodeType.EndZ:
                case RegexNodeType.End:
                    PushFC(new RegexFC(true));
                    break;

                default:
                    throw new ArgumentException(SR.GetString(SR.UnexpectedOpcode, NodeType.ToString()));
            }
        }
    }

    internal sealed class RegexFC 
    {
        internal RegexCharClass _cc;
        internal bool _nullable;
        internal bool _caseInsensitive;

        internal RegexFC(bool nullable) 
        {
            _cc = new RegexCharClass();
            _nullable = nullable;
        }

        internal RegexFC(char ch, bool not, bool nullable, bool caseInsensitive) {
            _cc = new RegexCharClass();

            if (not) {
                if (ch > 0)
                    _cc.AddRange('\0', (char)(ch - 1));
                if (ch < 0xFFFF)
                    _cc.AddRange((char)(ch + 1), '\uFFFF');
            }
            else {
                _cc.AddRange(ch, ch);
            }

            _caseInsensitive = caseInsensitive;
            _nullable = nullable;
        }

        internal RegexFC(string charClass, bool nullable, bool caseInsensitive) 
        {
            _cc = RegexCharClass.Parse(charClass);

            _nullable = nullable;
            _caseInsensitive = caseInsensitive;
        }

        internal bool AddFC(RegexFC fc, bool concatenate) 
        {
            if (!_cc.CanMerge || !fc._cc.CanMerge) 
            {
                return false;
            }
            
            if (concatenate) {
                if (!_nullable)
                    return true;

                if (!fc._nullable)
                    _nullable = false;
            }
            else {
                if (fc._nullable)
                    _nullable = true;
            }

            _caseInsensitive |= fc._caseInsensitive;
            _cc.AddCharClass(fc._cc);
            return true;
        }

        internal String GetFirstChars(CultureInfo culture) {
            if (_caseInsensitive)
                _cc.AddLowercase(culture);

            return _cc.ToStringClass();
        }
        
        internal bool IsCaseInsensitive() {
            return _caseInsensitive;
        }
    }

    internal sealed class RegexPrefix {
        internal String _prefix;
        internal bool _caseInsensitive;

        internal static RegexPrefix _empty = new RegexPrefix(String.Empty, false);

        internal RegexPrefix(String prefix, bool ci) {
            _prefix = prefix;
            _caseInsensitive = ci;
        }

        internal String Prefix {
            get {
                return _prefix;
            }
        }

        internal bool CaseInsensitive {
            get {
                return _caseInsensitive;
            }
        }
        internal static RegexPrefix Empty {
            get {
                return _empty;
            }
        }
    }
}
