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
    using System.Linq;

    public sealed class RegexTree 
    {
        public RegexTree(
            RegexNode root, 
            Dictionary<int, int> caps, 
            int[] capnumlist, 
            Dictionary<string, int> capnames,
            List<string> capslist, 
            RegexOptions opts)
        {
            this.root = root;
            _caps = caps;
            _capnumlist = capnumlist;
            _capnames = capnames;
            _capslist = capslist?.ToList();
            options = opts;
        }

        public RegexNode root;
        public Dictionary<int, int> _caps;
        public int[]  _capnumlist;
        public Dictionary<string, int> _capnames;
        public List<string>  _capslist;
        public RegexOptions options;

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
