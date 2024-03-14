//------------------------------------------------------------------------------
// <copyright file="RegexTree.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

// RegexTree is just a wrapper for a node tree with some
// global information attached.

namespace System.Text.RegularExpressions {

    using System.Collections;
    using System.Collections.Generic;

    public sealed class RegexTree 
    {
        internal RegexTree(RegexNode root, Dictionary<int, int> caps, int[] capnumlist, int captop, Dictionary<string, int> capnames, string[] capslist, RegexOptions opts)
        {
            this.root = root;
            _caps = caps;
            _capnumlist = capnumlist;
            _capnames = capnames;
            _capslist = capslist;
            _captop = captop;
            options = opts;
        }

        internal RegexNode root;
        internal Dictionary<int, int> _caps;
        internal int[]  _capnumlist;
        internal Dictionary<string, int> _capnames;
        internal string[]  _capslist;
        internal RegexOptions options;
        internal int       _captop;

#if DBG
        internal void Dump() {
            _root.Dump();
        }

        internal bool Debug {
            get {
                return(_options & RegexOptions.Debug) != 0;
            }
        }
#endif
    }
}
