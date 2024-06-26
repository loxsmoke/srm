﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.SRM
{
    /// <summary>
    /// Kinds of symbolic regexes
    /// </summary>
    public enum SymbolicRegexKind
    {
        StartAnchor = 0,
        EndAnchor = 1,
        Epsilon = 2,
        Singleton = 3,
        Or = 4,
        Concat = 5,
        Loop = 6,
        IfThenElse = 7,
        And = 8,
        WatchDog = 9,
        BOLAnchor = 10,
        EOLAnchor = 11,
    }

    /// <summary>
    /// Special purpose 0-width symbols that match corresponding anchors
    /// </summary>
    public enum BorderSymbol
    {
        StartOfLine = 0, // BOL
        EndOfLine = 1, // EOL
        Beg = 2, // Beg
        End = 3, // End
        Count = 4
    }

    /// <summary>
    /// Represents an AST node of a symbolic regex.
    /// </summary>
    public class SymbolicRegexNode<S>
    {
        internal SymbolicRegexBuilder<S> builder;
        internal SymbolicRegexKind kind;
        internal int lower = -1;
        internal int upper = -1;
        internal S set = default(S);
        internal ImmutableList<S> sequence = null;

        internal SymbolicRegexNode<S> left = null;
        internal SymbolicRegexNode<S> right = null;
        internal SymbolicRegexNode<S> iteCond = null;

        internal SymbolicRegexSet<S> alts = null;

        internal bool isNullable = false;
        public bool containsAnchors = false;

        int hashcode = -1;

        #region serialization

        /// <summary>
        /// Produce the serialized from of this symbolic regex node.
        /// </summary>
        public string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            Serialize(this, sb);
            return sb.ToString();
        }

        /// <summary>
        /// Append the serialized form of this symbolic regex node to the stringbuilder
        /// </summary>
        public static void Serialize(SymbolicRegexNode<S> node, System.Text.StringBuilder sb)
        {
            var solver = node.builder.solver;
            SymbolicRegexNode<S> next = node;
            while (next != null)
            {
                node = next;
                next = null;
                switch (node.kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            if (node.set.Equals(solver.True))
                                sb.Append(".");
                            else
                            {
                                sb.Append("[");
                                sb.Append(solver.SerializePredicate(node.set));
                                sb.Append("]");
                            }
                            return;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            if (node.isLazyLoop)
                                sb.Append("Z(");
                            else
                                sb.Append("L(");
                            sb.Append(node.lower.ToString());
                            sb.Append(",");
                            sb.Append(node.upper == int.MaxValue ? "*" : node.upper.ToString());
                            sb.Append(",");
                            Serialize(node.left, sb);
                            sb.Append(")");
                            return;
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            var elems = node.ToArray();
                            var elems_str = Array.ConvertAll(elems, x => x.Serialize());
                            var str = string.Join(",", elems_str);
                            sb.Append("S(");
                            sb.Append(str);
                            sb.Append(")");
                            return;
                        }
                    case SymbolicRegexKind.Epsilon:
                        {
                            sb.Append("E");
                            return;
                        }
                    case SymbolicRegexKind.Or:
                        {
                            sb.Append("D(");
                            node.alts.Serialize(sb);
                            sb.Append(")");
                            return;
                        }
                    case SymbolicRegexKind.And:
                        {
                            sb.Append("C(");
                            node.alts.Serialize(sb);
                            sb.Append(")");
                            return;
                        }
                    case SymbolicRegexKind.EndAnchor:
                        {
                            sb.Append("z");
                            return;
                        }
                    case SymbolicRegexKind.StartAnchor:
                        {
                            sb.Append("A");
                            return;
                        }
                    case SymbolicRegexKind.EOLAnchor:
                        {
                            sb.Append("$");
                            return;
                        }
                    case SymbolicRegexKind.BOLAnchor:
                        {
                            sb.Append("^");
                            return;
                        }
                    case SymbolicRegexKind.WatchDog:
                        {
                            sb.Append("#(" + node.lower + ")");
                            return;
                        }
                    default: // SymbolicRegexKind.IfThenElse:
                        {
                            sb.Append("I(");
                            Serialize(node.iteCond, sb);
                            sb.Append(",");
                            Serialize(node.left, sb);
                            sb.Append(",");
                            Serialize(node.right, sb);
                            sb.Append(")");
                            return;
                        }
                }
            }
        }

        /// <summary>
        /// Converts a concatenation into an array, 
        /// returns a non-concatenation in a singleton array.
        /// </summary>
        public SymbolicRegexNode<S>[] ToArray()
        {
            var list = new List<SymbolicRegexNode<S>>();
            AppendToList(this, list);
            return list.ToArray();
        }

        /// <summary>
        /// should only be used only if this is a concatenation node
        /// </summary>
        /// <returns></returns>
        static void AppendToList(SymbolicRegexNode<S> concat, List<SymbolicRegexNode<S>> list)
        {
            var node = concat;
            while (node.kind == SymbolicRegexKind.Concat)
            {
                if (node.left.kind == SymbolicRegexKind.Concat)
                    AppendToList(node.left, list);
                else
                    list.Add(node.left);
                node = node.right;
            }
            list.Add(node);
        }


        #endregion

        #region various properties
        /// <summary>
        /// Returns true if this is equivalent to .*
        /// </summary>
        public bool IsDotStar
        {
            get
            {
                return this.IsStar && this.left.kind == SymbolicRegexKind.Singleton &&
                    this.builder.solver.AreEquivalent(this.builder.solver.True, this.left.set);
            }
        }

        /// <summary>
        /// Returns true if this is equivalent to [0-[0]]
        /// </summary>
        public bool IsNothing
        {
            get
            {
                return this.kind == SymbolicRegexKind.Singleton &&
                    !this.builder.solver.IsSatisfiable(this.set);
            }
        }

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is max
        /// </summary>
        public bool IsStar => lower == 0 && upper == int.MaxValue;

        /// <summary>
        /// Returns true iff this loop has an upper bound
        /// </summary>
        public bool HasUpperBound => upper < int.MaxValue;

        /// <summary>
        /// Returns true iff this loop has a lower bound
        /// </summary>
        public bool HasLowerBound => lower > 0;

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 0 and upper bound is 1
        /// </summary>
        public bool IsMaybe => lower == 0 && upper == 1;

        /// <summary>
        /// Returns true if this is Epsilon
        /// </summary>
        public bool IsEpsilon => this.kind == SymbolicRegexKind.Epsilon;
        #endregion

        /// <summary>
        /// Alternatives of an OR
        /// </summary>
        public IEnumerable<SymbolicRegexNode<S>> Alts => alts;

        /// <summary>
        /// Gets the kind of the regex
        /// </summary>
        public SymbolicRegexKind Kind => kind;

        /// <summary>
        /// Number of alternative branches if this is an or-node. 
        /// If this is not an or-node then the value is 1.
        /// </summary>
        public int OrCount => kind == SymbolicRegexKind.Or ? alts.Count : 1;

        /// <summary>
        /// Left child of a binary node (the child of a unary node, the true-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Left => left;

        /// <summary>
        /// Right child of a binary node (the false-branch of an Ite-node)
        /// </summary>
        public SymbolicRegexNode<S> Right => right;

        /// <summary>
        /// The lower bound of a loop
        /// </summary>
        public int LowerBound => lower;

        /// <summary>
        /// The upper bound of a loop
        /// </summary>
        public int UpperBound => upper;

        /// <summary>
        /// The set of a singleton
        /// </summary>
        public S Set => set;

        /// <summary>
        /// Returns the number of top-level concatenation nodes.
        /// </summary>
        int _ConcatCount = -1;
        public int ConcatCount
        {
            get
            {
                if (_ConcatCount == -1)
                {
                    if (this.kind == SymbolicRegexKind.Concat)
                        _ConcatCount = left.ConcatCount + right.ConcatCount + 1;
                    else
                        _ConcatCount = 0;
                }
                return _ConcatCount;
            }
        }

        /// <summary>
        /// IfThenElse condition
        /// </summary>
        public SymbolicRegexNode<S> IteCond => iteCond;

        /// <summary>
        /// Returns true iff this is a loop whose lower bound is 1 and upper bound is max
        /// </summary>
        public bool IsPlus => lower == 1 && upper == int.MaxValue;

        /// <summary>
        /// Returns true iff this is a start-anchor
        /// </summary>
        public bool IsStartAnchor => this.kind == SymbolicRegexKind.StartAnchor;

        /// <summary>
        /// Returns true iff this is an anchor for detecting start of line (including first line or start of input)
        /// </summary>
        public bool IsBOLAnchor => this.kind == SymbolicRegexKind.BOLAnchor;

        /// <summary>
        /// Returns true iff this is an anchor for detecting end of input
        /// </summary>
        public bool IsEndAnchor => this.kind == SymbolicRegexKind.EndAnchor;

        /// <summary>
        /// Returns true iff this is an anchor for detecting end of line (including last line or end of input)
        /// </summary>
        public bool IsEOLAnchor => this.kind == SymbolicRegexKind.EOLAnchor;

        /// <summary>
        /// Returns true iff this is either a start-anchor or an end-anchor or EOLAnchor or BOLAnchor
        /// </summary>
        public bool IsAnchor => IsStartAnchor || IsEndAnchor || IsBOLAnchor || IsEOLAnchor;

        /// <summary>
        /// AST node of a symbolic regex
        /// </summary>
        /// <param name="builder">the builder</param>
        /// <param name="kind">what kind of node</param>
        /// <param name="left">left child</param>
        /// <param name="right">right child</param>
        /// <param name="lower">lower bound of a loop</param>
        /// <param name="upper">upper boubd of a loop</param>
        /// <param name="set">singelton set</param>
        /// <param name="iteCond">if-then-else condition</param>
        /// <param name="alts">alternatives set of a disjunction</param>
        /// <param name="seq">sequence of singleton sets</param>
        private SymbolicRegexNode(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right, int lower, int upper, S set, SymbolicRegexNode<S> iteCond, SymbolicRegexSet<S> alts)
        {
            this.builder = builder;
            this.kind = kind;
            this.left = left;
            this.right = right;
            this.lower = lower;
            this.upper = upper;
            this.set = set;
            this.iteCond = iteCond;
            this.alts = alts;
        }

        /// <summary>
        /// AST node of a symbolic regex
        /// </summary>
        /// <param name="builder">the builder</param>
        /// <param name="kind">what kind of node</param>
        /// <param name="left">left child</param>
        /// <param name="right">right child</param>
        private SymbolicRegexNode(SymbolicRegexBuilder<S> builder, SymbolicRegexKind kind, SymbolicRegexNode<S> left = null, SymbolicRegexNode<S> right = null)
        {
            this.builder = builder;
            this.kind = kind;
            this.left = left;
            this.right = right;
            this.lower = -1;
            this.upper = -1;
            this.set = default(S);
        }

        internal SymbolicRegexNode<S> ConcatWithoutNormalizing(SymbolicRegexNode<S> next)
        {
            return new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, this, next);
        }

        #region called only once, in the constructor of SymbolicRegexBuilder

        internal static SymbolicRegexNode<S> MkSingleton(SymbolicRegexBuilder<S> builder, S set)
        {
            return new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Singleton, null, null, -1, -1, set, null, null);
        }

        internal static SymbolicRegexNode<S> MkWatchDog(SymbolicRegexBuilder<S> builder, int length)
        {
            var wd = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.WatchDog, null, null, length, -1, default(S), null, null);
            wd.isNullable = true;
            return wd;
        }

        internal static SymbolicRegexNode<S> MkEpsilon(SymbolicRegexBuilder<S> builder)
        {
            var eps = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Epsilon);
            eps.isNullable = true;
            return eps;
        }

        internal static SymbolicRegexNode<S> MkStartAnchor(SymbolicRegexBuilder<S> builder)
        {
            var anchor = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.StartAnchor);
            anchor.containsAnchors = true;
            return anchor;
        }

        internal static SymbolicRegexNode<S> MkEndAnchor(SymbolicRegexBuilder<S> builder)
        {
            var anchor = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EndAnchor);
            anchor.containsAnchors = true;
            return anchor;
        }

        internal static SymbolicRegexNode<S> MkEolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var anchor = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.EOLAnchor);
            anchor.containsAnchors = true;
            return anchor;
        }

        internal static SymbolicRegexNode<S> MkBolAnchor(SymbolicRegexBuilder<S> builder)
        {
            var anchor = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.BOLAnchor);
            anchor.containsAnchors = true;
            return anchor;
        }

        internal static SymbolicRegexNode<S> MkDotStar(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body)
        {
            var loop = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, 0, int.MaxValue, default(S), null, null);
            loop.isNullable = true;
            return loop;
        }

        #endregion

        internal static SymbolicRegexNode<S> MkLoop(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> body, int lower, int upper, bool isLazy)
        {
            if (lower < 0 || upper < lower)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);

            var loop = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Loop, body, null, lower, upper, default(S), null, null);
            if (loop.lower == 0)
            {
                loop.isNullable = true;
            }
            else
            {
                loop.isNullable = body.isNullable;
            }
            loop.isLazyLoop = isLazy;
            loop.containsAnchors = body.containsAnchors;
            return loop;
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] choices)
        {
            return MkOr(builder, SymbolicRegexSet<S>.CreateDisjunction(builder, choices));
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, params SymbolicRegexNode<S>[] conjuncts)
        {
            var elems = SymbolicRegexSet<S>.CreateConjunction(builder, conjuncts);
            return MkAnd(builder, elems);
        }

        internal static SymbolicRegexNode<S> MkOr(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder.nothing;
            else if (alts.IsEverything)
                return builder.dotStar;
            else if (alts.IsSingleton)
                return alts.GetTheElement();

            var or = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Or, null, null, -1, -1, default(S), null, alts);
            or.isNullable = alts.IsNullable();
            or.containsAnchors = alts.ContainsAnchors();
            return or;
        }

        internal static SymbolicRegexNode<S> MkAnd(SymbolicRegexBuilder<S> builder, SymbolicRegexSet<S> alts)
        {
            if (alts.IsNothing)
                return builder.nothing;
            else if (alts.IsEverything)
                return builder.dotStar;
            else if (alts.IsSingleton)
                return alts.GetTheElement();

            var and = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.And, null, null, -1, -1, default(S), null, alts);
            and.isNullable = alts.IsNullable();
            and.containsAnchors = alts.ContainsAnchors();
            return and;
        }

        /// <summary>
        /// Only call MkConcat when left and right are flat, the resulting concat(left,right) is then also flat,
        /// </summary>
        internal static SymbolicRegexNode<S> MkConcat(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            SymbolicRegexNode<S> concat;
            if (left == builder.nothing || right == builder.nothing)
                return builder.nothing;
            else if (left.IsEpsilon)
                return right;
            else if (right.IsEpsilon)
                return left;
            else if (left.kind != SymbolicRegexKind.Concat)
            {
                concat = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left, right, -1, -1, default(S), null, null);
                concat.isNullable = left.isNullable && right.isNullable;
                concat.containsAnchors = left.containsAnchors || right.containsAnchors;
            }
            else
            {
                concat = right;
                var left_elems = left.ToArray();
                for (int i = left_elems.Length - 1; i >= 0; i = i - 1)
                {
                    var tmp = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.Concat, left_elems[i], concat, -1, -1, default(S), null, null);
                    tmp.isNullable = left_elems[i].isNullable && concat.isNullable;
                    tmp.containsAnchors = left_elems[i].containsAnchors || concat.containsAnchors;
                    concat = tmp;
                }
            }
            return concat;
        }
        private IEnumerable<SymbolicRegexNode<S>> EnumerateConcatElementsBackwards()
        {
            switch (this.kind)
            {
                case SymbolicRegexKind.Concat:
                    foreach (var elem in right.EnumerateConcatElementsBackwards())
                        yield return elem;
                    yield return left;
                    yield break;
                default:
                    yield return this;
                    yield break;
            }
        }

        internal static SymbolicRegexNode<S> MkIfThenElse(SymbolicRegexBuilder<S> builder, SymbolicRegexNode<S> cond, SymbolicRegexNode<S> left, SymbolicRegexNode<S> right)
        {
            if (right == builder.nothing)
            {
                return SymbolicRegexNode<S>.MkAnd(builder, cond, left);
            }
            else
            {
                var ite = new SymbolicRegexNode<S>(builder, SymbolicRegexKind.IfThenElse, left, right, -1, -1, default(S), cond, null);
                ite.isNullable = (cond.isNullable ? left.isNullable : right.isNullable);
                ite.containsAnchors = (cond.containsAnchors || left.containsAnchors || right.containsAnchors);
                return ite;
            }
        }

        /// <summary>
        /// Transform the symbolic regex so that all singletons have been intersected with the given predicate pred. 
        /// </summary>
        public SymbolicRegexNode<S> Restrict(S pred)
        {
            switch (kind)
            {
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return this;
                case SymbolicRegexKind.Singleton:
                    {
                        var newset = builder.solver.MkAnd(this.set, pred);
                        if (this.set.Equals(newset))
                            return this;
                        else
                            return builder.MkSingleton(newset);
                    }
                case SymbolicRegexKind.Loop:
                    {
                        var body = this.left.Restrict(pred);
                        if (body == this.left)
                            return this;
                        else
                            return builder.MkLoop(body, isLazyLoop, this.lower, this.upper);
                    }
                case SymbolicRegexKind.Concat:
                    {
                        var first = this.left.Restrict(pred);
                        var second = this.right.Restrict(pred);
                        if (first == this.left && second == this.right)
                            return this;
                        else
                            return builder.MkConcat(first, second);
                    }
                case SymbolicRegexKind.Or:
                    {
                        var choices = alts.Restrict(pred);
                        return builder.MkOr(choices);
                    }
                case SymbolicRegexKind.And:
                    {
                        var conjuncts = alts.Restrict(pred);
                        return builder.MkAnd(conjuncts);
                    }
                default: //ITE 
                    {
                        var truecase = this.left.Restrict(pred);
                        var falsecase = this.right.Restrict(pred);
                        var cond = this.iteCond.Restrict(pred);
                        if (truecase == this.left && falsecase == this.right && cond == this.iteCond)
                            return this;
                        else
                            return builder.MkIfThenElse(cond, truecase, falsecase);
                    }
            }
        }

        /// <summary>
        /// Returns the fixed matching length of the regex or -1 if the regex does not have a fixed matching length.
        /// </summary>
        public int GetFixedLength()
        {
            switch (kind)
            {
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                    return 0;
                case SymbolicRegexKind.Singleton:
                    return 1;
                case SymbolicRegexKind.Loop:
                    {
                        if (this.lower == this.upper)
                        {
                            var body_length = this.left.GetFixedLength();
                            if (body_length >= 0)
                                return this.lower * body_length;
                            else
                                return -1;
                        }
                        else
                            return -1;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        var left_length = this.left.GetFixedLength();
                        if (left_length >= 0)
                        {
                            var right_length = this.right.GetFixedLength();
                            if (right_length >= 0)
                                return left_length + right_length;
                        }
                        return -1;
                    }
                case SymbolicRegexKind.Or:
                    {
                        return alts.GetFixedLength();
                    }
                default: 
                    {
                        return -1;
                    }
            }
        }

        /// <summary>
        /// Replace all anchors (^ and $) in the symbolic regex with () and missing anchors with .*
        /// </summary>
        /// <param name="isBeg">if true (default) then this is the beginning borderline and missing ^ is replaced with .*</param>
        /// <param name="isEnd">if true (default) then this is the end borderline and missing $ is replaced with .*</param>
        /// <returns></returns>
        public SymbolicRegexNode<S> ReplaceAnchors(bool isBeg = true, bool isEnd = true)
        {
            return builder.RemoveAnchors(this, isBeg, isEnd);
        }

        /// <summary>
        /// Takes the derivative of the symbolic regex wrt elem. 
        /// Assumes that elem is either a minterm wrt the predicates of the whole regex or a singleton set.
        /// </summary>
        /// <param name="elem">given element wrt which the derivative is taken</param>
        /// <returns></returns>
        public SymbolicRegexNode<S> MkDerivative(S elem)
        {
            return builder.MkDerivative(elem, this);
        }

        /// <summary>
        /// Takes the derivative of the symbolic regex wrt an invisible border symbol.
        /// The symbol is ignored by any regex except a respective anchor.
        /// </summary>
        /// <param name="isStartLine">if true then start-line else end-line</param>
        /// <returns></returns>
        public SymbolicRegexNode<S> MkDerivativeForBorder(BorderSymbol borderSymbol)
        {
            return builder.MkDerivativeForBorder(borderSymbol, this);
        }

        /// <summary>
        /// true iff epsilon is accepted
        /// </summary>
        public bool IsNullable => isNullable;

        [NonSerialized]
        static int prime = 31;
        public override int GetHashCode()
        {
            if (hashcode == -1)
            {
                switch (kind)
                {
                    case SymbolicRegexKind.EndAnchor:
                    case SymbolicRegexKind.StartAnchor:
                    case SymbolicRegexKind.BOLAnchor:
                    case SymbolicRegexKind.EOLAnchor:
                    case SymbolicRegexKind.Epsilon:
                        hashcode = kind.GetHashCode();
                        break;
                    case SymbolicRegexKind.WatchDog:
                        hashcode = kind.GetHashCode() + lower;
                        break;
                    case SymbolicRegexKind.Loop:
                        hashcode = kind.GetHashCode() ^ left.GetHashCode() ^ lower ^ upper ^ isLazyLoop.GetHashCode();
                        break;
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        hashcode = kind.GetHashCode() ^ alts.GetHashCode();
                        break;
                    case SymbolicRegexKind.Concat:
                        hashcode = left.GetHashCode() + (prime * right.GetHashCode());
                        break;
                    case SymbolicRegexKind.Singleton:
                        hashcode = kind.GetHashCode() ^ set.GetHashCode();
                        break;
                    default: //if-then-else
                        hashcode = kind.GetHashCode() ^ iteCond.GetHashCode() ^ (left.GetHashCode() << 1) ^ (right.GetHashCode() << 2);
                        break;
                }
            }
            return hashcode;
        }

        public override bool Equals(object obj)
        {
            SymbolicRegexNode<S> that = obj as SymbolicRegexNode<S>;
            if (that == null)
            {
                return false;
            }
            else if (this == that)
            {
                return true;
            }
            else
            {
                if (this.kind != that.kind)
                    return false;
                switch (this.kind)
                {
                    case SymbolicRegexKind.Concat:
                        return this.left.Equals(that.left) && this.right.Equals(that.right);
                    case SymbolicRegexKind.Singleton:
                        return object.Equals(this.set, that.set);
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        return this.alts.Equals(that.alts);
                    case SymbolicRegexKind.Loop:
                        return this.lower == that.lower && this.upper == that.upper && this.left.Equals(that.left);
                    case SymbolicRegexKind.IfThenElse:
                        return this.iteCond.Equals(that.iteCond) && this.left.Equals(that.left) && this.right.Equals(that.right);
                    default: //otherwsie this.kind == that.kind implies they must be the same
                        return true;
                }
            }
        }

        string ToStringForLoop()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Singleton:
                    return ToString();
                default:
                    return "(" + ToString() + ")";
            }
        }

        internal string ToStringForAlts()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Concat:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.Loop:
                    return ToString();
                default:
                    return "(" + ToString() + ")";
            }
        }

        public override string ToString()
        {
            switch (kind)
            {
                case SymbolicRegexKind.EndAnchor:
                    return "\\z";
                case SymbolicRegexKind.StartAnchor:
                    return "\\A";
                case SymbolicRegexKind.BOLAnchor:
                    return "^";
                case SymbolicRegexKind.EOLAnchor:
                    return "$";
                case SymbolicRegexKind.Epsilon:
                    return "";
                case SymbolicRegexKind.WatchDog:
                    return "";
                case SymbolicRegexKind.Loop:
                    {
                        if (IsDotStar)
                            return ".*";
                        else if (IsStar)
                            return left.ToStringForLoop() + "*";
                        else if (IsBoundedLoop)
                            return left.ToStringForLoop() + "{" + lower + "," + upper + "}";
                        else
                            return left.ToStringForLoop() + "{" + lower + ",}";
                    }
                case SymbolicRegexKind.Or:
                    return alts.ToString();
                case SymbolicRegexKind.And:
                    return alts.ToString();
                case SymbolicRegexKind.Concat:
                    return left.ToString() + right.ToString();
                case SymbolicRegexKind.Singleton:
                    return builder.solver.SerializePredicate(set);
                default:
                    return "(TBD:if-then-else)";
            }
        }

        /// <summary>
        /// Returns the set of all predicates that occur in the regex
        /// </summary>
        public HashSet<S> GetPredicates()
        {
            var predicates = new HashSet<S>();
            CollectPredicates_helper(predicates);
            return predicates;
        }

        /// <summary>
        /// Collects all predicates that occur in the regex into the given set predicates
        /// </summary>
        void CollectPredicates_helper(HashSet<S> predicates)
        {
            switch (kind)
            {
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                    {
                        predicates.Add(builder.newLine.set);
                        return;
                    }
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                    return;
                case SymbolicRegexKind.Singleton:
                    {
                        predicates.Add(this.set);
                        return;
                    }
                case SymbolicRegexKind.Loop:
                    {
                        this.left.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    {
                        foreach (SymbolicRegexNode<S> sr in this.alts)
                            sr.CollectPredicates_helper(predicates);
                        return;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        left.CollectPredicates_helper(predicates);
                        right.CollectPredicates_helper(predicates);
                        return;
                    }
                default: //ITE
                    {
                        this.iteCond.CollectPredicates_helper(predicates);
                        this.left.CollectPredicates_helper(predicates);
                        this.right.CollectPredicates_helper(predicates);
                        return;
                    }
            }
        }

        /// <summary>
        /// Compute all the minterms from the predicates in this regex.
        /// If S implements IComparable then sort the result in increasing order.
        /// </summary>
        public S[] ComputeMinterms()
        {
            var predicates = GetPredicates().ToList();
            var mt = builder.solver.GenerateMinterms(predicates.ToArray()).Select(pair => pair.Item2).ToList();

            //there must be at least one minterm
            if (mt.Count == 0)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);

            if (mt[0] is IComparable)
                mt.Sort();
            var minterms = mt.ToArray();
            return minterms;
        }

        /// <summary>
        /// Create the reverse of this regex
        /// </summary>
        public SymbolicRegexNode<S> Reverse()
        {
            switch (kind)
            {
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                    return this;
                case SymbolicRegexKind.WatchDog:
                    return builder.epsilon;
                case SymbolicRegexKind.Loop:
                    return builder.MkLoop(this.left.Reverse(), this.isLazyLoop, this.lower, this.upper);
                case SymbolicRegexKind.Concat:
                    {
                        var rev = left.Reverse();
                        var rest = this.right;
                        while (rest.kind == SymbolicRegexKind.Concat)
                        {
                            var rev1 = rest.left.Reverse();
                            rev = builder.MkConcat(rev1, rev);
                            rest = rest.right;
                        }
                        var restr = rest.Reverse();
                        rev = builder.MkConcat(restr, rev);
                        return rev;
                    }
                case SymbolicRegexKind.Or:
                    {
                        var rev = builder.MkOr(alts.Reverse());
                        return rev;
                    }
                case SymbolicRegexKind.And:
                    {
                        var rev = builder.MkAnd(alts.Reverse());
                        return rev;
                    }
                default: //if-then-else
                    return builder.MkIfThenElse(iteCond.Reverse(), left.Reverse(), right.Reverse());
            }
        }

        internal bool StartsWithLoop(int upperBoundLowestValue = 1)
        {
            switch (kind)
            {
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Singleton: 
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                    return false;
                case SymbolicRegexKind.Loop:
                    return (this.upper < int.MaxValue) && (this.upper > upperBoundLowestValue);
                case SymbolicRegexKind.Concat:
                    return (this.left.StartsWithLoop(upperBoundLowestValue) ||
                        (this.left.isNullable && this.right.StartsWithLoop(upperBoundLowestValue)));
                case SymbolicRegexKind.Or:
                    return alts.StartsWithLoop(upperBoundLowestValue);
                default:
                    throw new NotImplementedException();
            }
        }

        int enabledBoundedLoopCount = -1;

        internal int EnabledBoundedLoopCount
        {
            get
            {
                if (enabledBoundedLoopCount == -1)
                {
                    switch (kind)
                    {
                        case SymbolicRegexKind.EndAnchor:
                        case SymbolicRegexKind.StartAnchor:
                        case SymbolicRegexKind.EOLAnchor:
                        case SymbolicRegexKind.BOLAnchor:
                        case SymbolicRegexKind.Singleton:
                        case SymbolicRegexKind.WatchDog:
                        case SymbolicRegexKind.Epsilon:
                            {
                                enabledBoundedLoopCount = 0;
                                break;
                            }
                        case SymbolicRegexKind.Loop:
                            {
                                //nr of loops in the body
                                int n = this.left.EnabledBoundedLoopCount;
                                if ((this.upper < int.MaxValue) && (this.upper > 0))
                                    n += 1;
                                enabledBoundedLoopCount = n;
                                break;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                int n = this.left.EnabledBoundedLoopCount;
                                //if (this.left.IsNullable())
                                //    n += this.right.EnabledBoundedLoopCount;
                                enabledBoundedLoopCount = n;
                                break;
                            }
                        case SymbolicRegexKind.Or:
                            {
                                enabledBoundedLoopCount = alts.EnabledBoundedLoopCount;
                                break;
                            }
                        default:
                            throw new NotImplementedException(kind.ToString());
                    }
                }
                return enabledBoundedLoopCount;
            }
        }

        internal int EnabledBoundedLoopValue()
        {

            switch (kind)
            {
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.Epsilon:
                    {
                        return 0;
                    }
                case SymbolicRegexKind.Loop:
                    {
                        if (this.upper < int.MaxValue)
                            return this.upper;
                        else
                            return 0;
                    }
                case SymbolicRegexKind.Concat:
                    {
                        return this.left.EnabledBoundedLoopValue();
                    }
                case SymbolicRegexKind.Or:
                    {
                        foreach (var alt in this.alts)
                        {
                            var k = alt.EnabledBoundedLoopValue();
                            if (k > 0)
                                return k;
                        }
                        return 0;
                    }
                default:
                    throw new NotImplementedException(kind.ToString());
            }
        }

        /// <summary>
        /// Unwind lower loop boundaries
        /// </summary>
        internal SymbolicRegexNode<S> Simplify()
        {
            switch (kind)
            {
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.BOLAnchor:
                case SymbolicRegexKind.EOLAnchor:
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.Singleton:
                case SymbolicRegexKind.WatchDog:
                    return this;
                case SymbolicRegexKind.Concat:
                    return builder.MkConcat(left.Simplify(), right.Simplify());
                case SymbolicRegexKind.Or:
                    return builder.MkOr(alts.Simplify());
                case SymbolicRegexKind.And:
                    return builder.MkAnd(alts.Simplify());
                case SymbolicRegexKind.Loop:
                    {
                        var body = this.left.Simplify();
                        //we know that lower <= upper
                        //so diff >= 0
                        int diff = (this.upper == int.MaxValue ? int.MaxValue : upper - lower);
                        var res = (diff == 0 ? builder.epsilon : builder.MkLoop(body, isLazyLoop, 0, diff));
                        for (int i = 0; i < lower; i++)
                            res = builder.MkConcat(body, res);
                        return res;
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Only valid to call if there is a single bounded loop
        /// </summary>
        internal SymbolicRegexNode<S> DecrementBoundedLoopCount(bool makeZero = false)
        {
            if (EnabledBoundedLoopCount != 1)
                return this;
            else
            {
                switch (kind)
                {
                    case SymbolicRegexKind.EndAnchor:
                    case SymbolicRegexKind.StartAnchor:
                    case SymbolicRegexKind.EOLAnchor:
                    case SymbolicRegexKind.BOLAnchor:
                    case SymbolicRegexKind.Singleton:
                    case SymbolicRegexKind.WatchDog:
                    case SymbolicRegexKind.Epsilon:
                        {
                            return this;
                        }
                    case SymbolicRegexKind.Loop:
                        {
                            if ((lower == 0) && (upper > 0) && (upper < int.MaxValue))
                            {
                                //must be this loop
                                if (makeZero)
                                    return builder.epsilon;
                                else
                                {
                                    int upper1 = upper - 1;
                                    return builder.MkLoop(this.left, this.isLazyLoop, 0, upper1);
                                }
                            }
                            else
                            {
                                return this;
                            }
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            return builder.MkConcat(left.DecrementBoundedLoopCount(makeZero), right);
                        }
                    case SymbolicRegexKind.Or:
                        {
                            return builder.MkOr(alts.DecrementBoundedLoopCount(makeZero));
                        }
                    default:
                        throw new NotImplementedException(kind.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the string prefix that the regex must match or the empty string if such a prefix does not exist.
        /// </summary>
        internal string GetFixedPrefix(CharSetSolver css, out bool ignoreCase)
        {
            var pref = GetFixedPrefix_(css, out ignoreCase);
            int i = pref.IndexOf('I');
            int k = pref.IndexOf('K');
            if (ignoreCase && (i != -1 || k != -1))
            {
                //eliminate I and K to avoid possible semantic discrepancy with later search
                //due to \u0130 (capital I with dot above, İ,  in regex same as i modulo ignore case)
                //due to \u212A (Kelvin sign, in regex same as k under ignore case)
                //but these do not match with string.IndexOf modulo ignore case
                if (k == -1)
                    return pref.Substring(0, i);
                else if (i == -1)
                    return pref.Substring(0, k);
                else
                    return pref.Substring(0, (i < k ? i : k));
            }
            else
            {
                return pref;
            }
        }

        string GetFixedPrefix_(CharSetSolver css, out bool ignoreCase)
        {
            #region compute fixedPrefix
            S[] prefix = GetPrefix();
            if (prefix.Length == 0)
            {
                ignoreCase = false;
                return string.Empty;
            }
            else
            {
                BDD[] bdds = Array.ConvertAll(prefix, p => builder.solver.ConvertToCharSet(css, p));
                if (Array.TrueForAll(bdds, x => css.IsSingleton(x)))
                {
                    //all elements are singletons
                    char[] chars = Array.ConvertAll(bdds, x => (char)x.GetMin());
                    ignoreCase = false;
                    return new string(chars);
                }
                else
                {
                    //maps x to itself if x is invariant under ignoring case
                    //maps x to False otherwise
                    Func<BDD, BDD> F = x =>
                    {
                        char c = (char)x.GetMin();
                        var y = css.MkCharConstraint(c, true);
                        if (x == y)
                            return x;
                        else
                            return css.False;
                    };
                    BDD[] bdds1 = Array.ConvertAll(bdds, x => F(x));
                    if (Array.TrueForAll(bdds1, x => !x.IsEmpty))
                    {
                        //all elements are singletons up-to-ignoring-case
                        //choose representatives
                        char[] chars = Array.ConvertAll(bdds, x => (char)x.GetMin());
                        ignoreCase = true;
                        return new string(chars);
                    }
                    else
                    {
                        List<char> elems = new List<char>();
                        //extract prefix of singletons
                        for (int i = 0; i < bdds.Length; i++)
                        {
                            if (css.IsSingleton(bdds[i]))
                                elems.Add((char)bdds[i].GetMin());
                            else
                                break;
                        }
                        List<char> elemsI = new List<char>();
                        //extract prefix up-to-ignoring-case 
                        for (int i = 0; i < bdds1.Length; i++)
                        {
                            if (bdds1[i].IsEmpty)
                                break;
                            else
                                elemsI.Add((char)bdds1[i].GetMin());
                        }
                        //TBD: these heuristics should be evaluated more
                        #region different cases of fixed prefix
                        if (elemsI.Count > elems.Count)
                        {
                            ignoreCase = true;
                            return new string(elemsI.ToArray());
                        }
                        else if (elems.Count > 0)
                        {
                            ignoreCase = false;
                            return new string(elems.ToArray());
                        }
                        else if (elemsI.Count > 0)
                        {
                            ignoreCase = true;
                            return new string(elemsI.ToArray());
                        }
                        else
                        {
                            ignoreCase = false;
                            return string.Empty;
                        }
                        #endregion
                    }
                }
            }
            #endregion
        }

        internal const int maxPrefixLength = 5;
        internal S[] GetPrefix()
        {
            return GetPrefixSequence(ImmutableList<S>.Empty, maxPrefixLength).ToArray();
        }

        ImmutableList<S> GetPrefixSequence(ImmutableList<S> pref, int lengthBound)
        {
            if (lengthBound == 0)
            {
                return pref;
            }
            else
            {
                switch (this.kind)
                {
                    case SymbolicRegexKind.Singleton:
                        {
                            return pref.Add(this.set);
                        }
                    case SymbolicRegexKind.Concat:
                        {
                            if (this.left.kind == SymbolicRegexKind.Singleton)
                                return this.right.GetPrefixSequence(pref.Add(this.left.set), lengthBound - 1);
                            else
                                return pref;
                        }
                    case SymbolicRegexKind.Or:
                    case SymbolicRegexKind.And:
                        {
                            var enumerator = alts.GetEnumerator();
                            enumerator.MoveNext();
                            var alts_prefix = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                            while (!alts_prefix.IsEmpty && enumerator.MoveNext())
                            {
                                var p = enumerator.Current.GetPrefixSequence(ImmutableList<S>.Empty, lengthBound);
                                var prefix_length = alts_prefix.TakeWhile((x, i) => i < p.Count && x.Equals(p[i])).Count();
                                alts_prefix = alts_prefix.RemoveRange(prefix_length, alts_prefix.Count - prefix_length);
                            }
                            return pref.AddRange(alts_prefix);
                        }
                    default:
                        {
                            return pref;
                        }
                }
            }
        }

        ///// <summary>
        ///// If this node starts with a loop other than star or plus then 
        ///// returns the nonnegative id of the associated counter else returns -1
        ///// </summary>
        //public int CounterId
        //{
        //    get
        //    {
        //        if (this.kind == SymbolicRegexKind.Loop && !this.IsStar && !this.IsPlus)
        //            return this.builder.GetCounterId(this);
        //        else if (this.kind == SymbolicRegexKind.Concat)
        //            return left.CounterId;
        //        else
        //            return -1;
        //    }
        //}

        //0 means value is not computed, 
        //-1 means this is not a sequence of singletons
        //1 means it is a sequence of singletons
        internal int sequenceOfSingletons_count = 0;

        /// <summary>
        /// true if this node is a lazy loop
        /// </summary>
        internal bool isLazyLoop = false;

        internal bool IsSequenceOfSingletons
        {
            get
            {
                if (sequenceOfSingletons_count == 0)
                {
                    var node = this;
                    int k = 1;
                    while (node.kind == SymbolicRegexKind.Concat && node.left.kind == SymbolicRegexKind.Singleton)
                    {
                        node = node.right;
                        k += 1;
                    }
                    if (node.kind == SymbolicRegexKind.Singleton)
                    {
                        node.sequenceOfSingletons_count = 1;
                        node = this;
                        while (node.kind == SymbolicRegexKind.Concat)
                        {
                            node.sequenceOfSingletons_count = k;
                            node = node.right;
                            k = k - 1;
                        }
                    }
                    else
                    {
                        node.sequenceOfSingletons_count = -1;
                        node = this;
                        while (node.kind == SymbolicRegexKind.Concat && node.left.kind == SymbolicRegexKind.Singleton)
                        {
                            node.sequenceOfSingletons_count = -1;
                            node = node.right;
                        }
                    }
                }
                return sequenceOfSingletons_count > 0;
            }
        }

        /// <summary>
        /// Gets the predicate that covers all elements that make some progress. 
        /// </summary>
        public S GetStartSet(ICharAlgebra<S> algebra)
        {
            switch (kind)
            {
                case SymbolicRegexKind.Epsilon:
                case SymbolicRegexKind.WatchDog:
                case SymbolicRegexKind.EndAnchor:
                case SymbolicRegexKind.StartAnchor:
                case SymbolicRegexKind.EOLAnchor:
                    return algebra.False;
                case SymbolicRegexKind.BOLAnchor:
                    return builder.newLine.set;
                case SymbolicRegexKind.Singleton:
                    return this.set;
                //case SymbolicRegexKind.Sequence:
                //    return this.sequence.First;
                case SymbolicRegexKind.Loop:
                    return this.left.GetStartSet(algebra);
                case SymbolicRegexKind.Concat:
                    {
                        var startSet = this.left.GetStartSet(algebra);
                        if (left.isNullable || left.IsStartAnchor || left.IsBOLAnchor)
                        {
                            var set2 = this.right.GetStartSet(algebra);
                            startSet = algebra.MkOr(startSet, set2);
                        }
                        return startSet;
                    }
                case SymbolicRegexKind.Or:
                    {
                        S startSet = algebra.False;
                        foreach (var alt in alts)
                            startSet = algebra.MkOr(startSet, alt.GetStartSet(algebra));
                        return startSet;
                    }
                case SymbolicRegexKind.And:
                    {
                        S startSet = algebra.True;
                        foreach (var alt in alts)
                            startSet = algebra.MkAnd(startSet, alt.GetStartSet(algebra));
                        return startSet;
                    }
                default: //if-then-else
                    {
                        S startSet = algebra.MkOr(iteCond.GetStartSet(algebra), algebra.MkOr(left.GetStartSet(algebra), right.GetStartSet(algebra)));
                        return startSet;
                    }
            }
        }

        /// <summary>
        /// Returns true iff there exists a node that satisfies the predicate
        /// </summary>
        public bool ExistsNode(Predicate<SymbolicRegexNode<S>> pred)
        {
            if (pred(this))
                return true;

            switch (kind)
            {
                case SymbolicRegexKind.Concat:
                    return left.ExistsNode(pred) || right.ExistsNode(pred);
                case SymbolicRegexKind.Or:
                case SymbolicRegexKind.And:
                    foreach (var node in this.alts)
                        if (node.ExistsNode(pred))
                            return true;
                    return false;
                case SymbolicRegexKind.Loop:
                    return left.ExistsNode(pred);
                default:
                    return false;
            }
        }

        public int CounterId => builder.GetCounterId(this);

        /// <summary>
        /// Returns true if this is a loop with an upper bound
        /// </summary>
        public bool IsBoundedLoop
        {
            get
            {
                return (this.kind == SymbolicRegexKind.Loop && this.upper < int.MaxValue);
            }
        }

        /// <summary>
        /// Returns true if the match-end of this regex can be determined with a 
        /// single pass from the start. 
        /// </summary>
        public bool IsSinglePass
        {
            get
            {
                if (this.IsSequenceOfSingletons)
                    return true;
                else
                {
                    switch (kind)
                    {
                        case SymbolicRegexKind.Or:
                            {
                                foreach (var member in alts)
                                    if (!member.IsSinglePass)
                                        return false;
                                return true;
                            }
                        case SymbolicRegexKind.Concat:
                            {
                                return left.IsSinglePass && right.IsSinglePass;
                            }
                        default:
                            return false;
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the regex contains a lazy loop
        /// </summary>
        public bool CheckIfContainsLazyLoop()
        {
            return this.ExistsNode(node => (node.kind == SymbolicRegexKind.Loop && node.isLazyLoop));
        }

        /// <summary>
        /// Returns true if there are no loops or if all loops are lazy. 
        /// </summary>
        public bool CheckIfAllLoopsAreLazy()
        {
            bool existsEagerLoop =  this.ExistsNode(node => (node.kind == SymbolicRegexKind.Loop && !node.IsMaybe && !node.isLazyLoop));
            return !existsEagerLoop;
        }

        /// <summary>
        /// Returns true if there is a loop
        /// </summary>
        public bool CheckIfLoopExists()
        {
            bool existsLoop = this.ExistsNode(node => (node.kind == SymbolicRegexKind.Loop));
            return existsLoop;
        }
    }

    /// <summary>
    /// The kind of a symbolic regex set
    /// </summary>
    public enum SymbolicRegexSetKind { Conjunction, Disjunction };

    /// <summary>
    /// Represents a set of symbolic regexes that is either a disjunction or a conjunction
    /// </summary>
    public class SymbolicRegexSet<S> : IEnumerable<SymbolicRegexNode<S>>
    {
        internal static bool optimizeLoops = true;
        internal SymbolicRegexBuilder<S> builder;

        HashSet<SymbolicRegexNode<S>> set;
        //if the set kind is disjunction then
        //symbolic regex A{0,k}B is stored as (A,B) -> k
        //symbolic regex A{0,k} is stored as (A,()) -> k
        Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int> loops;

        internal SymbolicRegexSetKind kind;

        int hashCode = 0;

        //#region serialization
        ///// <summary>
        ///// Serialize
        ///// </summary>
        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    //var ctx = context.Context as SymbolicRegexBuilder<S>;
        //    //if (ctx == null || ctx != builder)
        //    //    throw new AutomataException(AutomataExceptionKind.InvalidSerializationContext);
        //    info.AddValue("loops", loops);
        //    info.AddValue("set", set);
        //    info.AddValue("kind", kind);
        //}

        ///// <summary>
        ///// Deserialize
        ///// </summary>
        //public SymbolicRegexSet(SerializationInfo info, StreamingContext context)
        //{
        //    builder = context.Context as SymbolicRegexBuilder<S>;
        //    if (builder == null)
        //        throw new AutomataException(AutomataExceptionKind.SerializationNotSupported);

        //    kind = (SymbolicRegexSetKind)info.GetValue("kind", typeof(SymbolicRegexSetKind));
        //    set = (HashSet<SymbolicRegexNode<S>>)info.GetValue("set", typeof(HashSet<SymbolicRegexNode<S>>));
        //    loops = (Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>)info.GetValue("loops", typeof(Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>));
        //}
        //#endregion

        public SymbolicRegexSetKind Kind
        {
            get { return kind; }
        }

        /// <summary>
        /// if >= 0 then the maximal length of a watchdog in the set
        /// </summary>
        internal int watchdog = -1;

        /// <summary>
        /// Denotes the empty conjunction
        /// </summary>
        public bool IsEverything
        {
            get { return this.kind == SymbolicRegexSetKind.Conjunction && this.set.Count == 0 && this.loops.Count == 0; }
        }

        /// <summary>
        /// Denotes the empty disjunction
        /// </summary>
        public bool IsNothing
        {
            get { return this.kind == SymbolicRegexSetKind.Disjunction && this.set.Count == 0 && this.loops.Count == 0; }
        }

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexSetKind kind)
        {
            this.builder = builder;
            this.kind = kind;
            this.set = new HashSet<SymbolicRegexNode<S>>();
            this.loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
        }

        private SymbolicRegexSet(SymbolicRegexBuilder<S> builder, SymbolicRegexSetKind kind, HashSet<SymbolicRegexNode<S>> set, Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int> loops)
        {
            this.builder = builder;
            this.kind = kind;
            this.set = set;
            this.loops = loops;
        }

        internal static SymbolicRegexSet<S> MkFullSet(SymbolicRegexBuilder<S> builder)
        {
            return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Conjunction);
        }

        internal static SymbolicRegexSet<S> MkEmptySet(SymbolicRegexBuilder<S> builder)
        {
            return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Disjunction);
        }

        static internal SymbolicRegexSet<S> CreateDisjunction(SymbolicRegexBuilder<S> builder, IEnumerable<SymbolicRegexNode<S>> elems)
        {
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
            var other = new HashSet<SymbolicRegexNode<S>>();
            int watchdog = -1;
            if (optimizeLoops)
            { 
                foreach (var elem in elems)
                {
                    //keep track of maximal watchdog in the set
                    if (elem.kind == SymbolicRegexKind.WatchDog && elem.lower > watchdog)
                        watchdog = elem.lower;

                    #region start foreach
                    if (elem == builder.dotStar)
                        return builder.fullSet;
                    else if (elem != builder.nothing)
                    {
                        switch (elem.kind)
                        {
                            case SymbolicRegexKind.Or:
                                {
                                    foreach (var alt in elem.alts)
                                    {
                                        if (alt.kind == SymbolicRegexKind.Loop && alt.lower == 0)
                                        {
                                            var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(alt.left, builder.epsilon);
                                            //map to the maximal of the upper bounds
                                            int cnt;
                                            if (loops.TryGetValue(pair, out cnt))
                                            {
                                                if (cnt < alt.upper)
                                                    loops[pair] = alt.upper;
                                            }
                                            else
                                            {
                                                loops[pair] = alt.upper;
                                            }
                                        }
                                        else if (alt.kind == SymbolicRegexKind.Concat && alt.left.kind == SymbolicRegexKind.Loop && alt.left.lower == 0)
                                        {
                                            var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(alt.left.left, alt.right);
                                            //map to the maximal of the upper bounds
                                            int cnt;
                                            if (loops.TryGetValue(pair, out cnt))
                                            {
                                                if (cnt < alt.left.upper)
                                                    loops[pair] = alt.left.upper;
                                            }
                                            else
                                            {
                                                loops[pair] = alt.left.upper;
                                            }
                                        }
                                        else
                                        {
                                            other.Add(alt);
                                        }
                                    }
                                    break;
                                }
                            case SymbolicRegexKind.Loop:
                                {
                                    if (elem.kind == SymbolicRegexKind.Loop && elem.lower == 0)
                                    {
                                        var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(elem.left, builder.epsilon);
                                        //map the body of the loop (elem.left) to the maximal of the upper bounds
                                        int cnt;
                                        if (loops.TryGetValue(pair, out cnt))
                                        {
                                            if (cnt < elem.upper)
                                                loops[pair] = elem.upper;
                                        }
                                        else
                                        {
                                            loops[pair] = elem.upper;
                                        }
                                    }
                                    else
                                    {
                                        other.Add(elem);
                                    }
                                    break;
                                }
                            case SymbolicRegexKind.Concat:
                                {
                                    if (elem.kind == SymbolicRegexKind.Concat && elem.left.kind == SymbolicRegexKind.Loop && elem.left.lower == 0)
                                    {
                                        var pair = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(elem.left.left, elem.right);
                                        //map to the maximal of the upper bounds
                                        int cnt;
                                        if (loops.TryGetValue(pair, out cnt))
                                        {
                                            if (cnt < elem.left.upper)
                                                loops[pair] = elem.left.upper;
                                        }
                                        else
                                        {
                                            loops[pair] = elem.left.upper;
                                        }
                                    }
                                    else
                                    {
                                        other.Add(elem);
                                    }
                                    break;
                                }
                            default:
                                {
                                    other.Add(elem);
                                    break;
                                }
                        }
                    }
                    #endregion
                }
                //if any element of other is covered in loops then omit it
                var others1 = new HashSet<SymbolicRegexNode<S>>();
                foreach (var sr in other)
                {
                    var key = new Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>(sr, builder.epsilon);
                    if (loops.ContainsKey(key))
                        others1.Add(sr);
                }
                foreach (var pair in loops)
                {
                    if (other.Contains(pair.Key.Item2))
                        others1.Add(pair.Key.Item2);
                }
                other.ExceptWith(others1);
            }
            else
            {
                foreach (var elem in elems)
                {
                    if (elem.kind == SymbolicRegexKind.Or)
                    {
                        other.UnionWith(elem.alts);
                    }
                    else
                    {
                        other.Add(elem);
                    }
                }
            }
            if (other.Count == 0 && loops.Count == 0)
                return builder.emptySet;
            else
            {
                var disj = new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Disjunction, other, loops);
                disj.watchdog = watchdog;
                return disj;
            }
        }

        static internal SymbolicRegexSet<S> CreateConjunction(SymbolicRegexBuilder<S> builder, IEnumerable<SymbolicRegexNode<S>> elems)
        {
            var loops = new Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>();
            var conjuncts = new HashSet<SymbolicRegexNode<S>>();
            foreach (var elem in elems)
            {
                if (elem == builder.nothing)
                    return builder.emptySet;
                if (elem == builder.dotStar)
                    continue;
                if (elem.kind == SymbolicRegexKind.And)
                {
                    conjuncts.UnionWith(elem.alts);
                }
                else
                {
                    conjuncts.Add(elem);
                }
            }
            if (conjuncts.Count == 0)
                return builder.fullSet;
            else
                return new SymbolicRegexSet<S>(builder, SymbolicRegexSetKind.Conjunction, conjuncts, loops);
        }

        IEnumerable<SymbolicRegexNode<S>> RestrictElems(S pred)
        {
            foreach (var elem in this)
                yield return elem.Restrict(pred);
        }

        public SymbolicRegexSet<S> Restrict(S pred)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, RestrictElems(pred));
            else
                return CreateConjunction(builder, RestrictElems(pred));
        }

        /// <summary>
        /// How many elements are there in this set
        /// </summary>
        public int Count => set.Count + loops.Count;

        /// <summary>
        /// True iff the set is a singleton
        /// </summary>
        public bool IsSingleton => Count == 1;

        public bool IsNullable(bool isFirst = false, bool isLast = false)
        {
            var e = this.GetEnumerator();
            if (kind == SymbolicRegexSetKind.Disjunction)
            {
                #region some element must be nullable
                while (e.MoveNext())
                {
                    if (e.Current.IsNullable)
                        return true;
                }
                return false;
                #endregion
            }
            else
            {
                #region  all elements must be nullable
                while (e.MoveNext())
                {
                    if (!e.Current.IsNullable)
                        return false;
                }
                return true;
                #endregion
            }
        }

        public override int GetHashCode()
        {
            if (hashCode == 0)
            {
                hashCode = this.kind.GetHashCode();
                var e = set.GetEnumerator();
                while (e.MoveNext())
                {
                    hashCode = hashCode ^ e.Current.GetHashCode();
                }
                e.Dispose();
                var e2 = loops.GetEnumerator();
                while (e2.MoveNext())
                {
                    hashCode = (hashCode ^ (e2.Current.Key.GetHashCode() + e2.Current.Value));
                }
            }
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            var that = obj as SymbolicRegexSet<S>;
            if (that == null)
                return false;
            if (this.kind != that.kind)
                return false;
            if (this.set.Count != that.set.Count)
                return false;
            if (this.loops.Count != that.loops.Count)
                return false;
            if (this.set.Count > 0 && !this.set.SetEquals(that.set))
                return false;
            var e1 = this.loops.GetEnumerator();
            while (e1.MoveNext())
            {
                int cnt;
                if (!that.loops.TryGetValue(e1.Current.Key, out cnt))
                    return false;
                if (cnt != e1.Current.Value)
                    return false;
            }
            e1.Dispose();
            return true;
        }

        public override string ToString()
        {
            string res = "";
            var e = this.GetEnumerator();
            var R = new List<string>();
            while (e.MoveNext())
                R.Add(e.Current.ToStringForAlts());
            if (R.Count == 0)
                return res;
            if (kind == SymbolicRegexSetKind.Disjunction)
            {
                #region display as R[0]|R[1]|...
                for (int i = 0; i < R.Count; i++)
                {
                    if (res != "")
                        res += "|";
                    res += R[i].ToString();
                }
                #endregion
            }
            else
            {
                #region display using if-then-else construct: (?(A)(B)|[0-[0]]) to represent intersect(A,B)
                res = R[R.Count - 1].ToString();
                for (int i = R.Count - 2; i >= 0; i--)
                {
                    //unfortunately [] is an invalid character class expression, using [0-[0]] instead
                    res = string.Format("(?({0})({1})|{2})", R[i].ToString(), res, "[0-[0]]");
                }
                #endregion
            }
            //if (this.Count > 1 && kind == SymbolicRegexSetKind.Disjunction)
            //    //add extra parentesis to enclose the disjunction
            //    return "(" + res + ")";
            //else
            return res;
        }

        internal SymbolicRegexNode<S>[] ToArray(SymbolicRegexBuilder<S> builder)
        {
            List<SymbolicRegexNode<S>> elemsL = new List<SymbolicRegexNode<S>>(this);
            SymbolicRegexNode<S>[] elems = elemsL.ToArray();
            return elems;
        }

        IEnumerable<SymbolicRegexNode<S>> RemoveAnchorsElems(SymbolicRegexBuilder<S> builder, bool isBeg, bool isEnd)
        {
            foreach (var elem in this)
                yield return elem.ReplaceAnchors(isBeg, isEnd);
        }

        public SymbolicRegexSet<S> RemoveAnchors(bool isBeg, bool isEnd)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, RemoveAnchorsElems(builder, isBeg, isEnd));
            else
                return CreateConjunction(builder, RemoveAnchorsElems(builder, isBeg, isEnd));
        }

        internal SymbolicRegexSet<S> MkDerivative(S elem)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, MkDerivativesOfElems(elem));
            else
                return CreateConjunction(builder, MkDerivativesOfElems(elem));
        }

        internal SymbolicRegexSet<S> MkDerivativesForBorder(BorderSymbol borderSymbol)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, MkDerivativesForBorder_(borderSymbol));
            else
                return CreateConjunction(builder, MkDerivativesForBorder_(borderSymbol));
        }

        IEnumerable<SymbolicRegexNode<S>> MkDerivativesOfElems(S elem)
        {
            foreach (var s in this)
                yield return s.MkDerivative(elem);
        }

        IEnumerable<SymbolicRegexNode<S>> MkDerivativesForBorder_(BorderSymbol borderSymbol)  
        {
            foreach (var s in this)
                yield return s.builder.MkDerivativeForBorder(borderSymbol, s);
        }

        IEnumerable<SymbolicRegexNode<T>> TransformElems<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
        {
            foreach (var sr in this)
                yield return builder.Transform(sr, builderT, predicateTransformer);
        }

        internal SymbolicRegexSet<T> Transform<T>(SymbolicRegexBuilder<T> builderT, Func<S, T> predicateTransformer)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return SymbolicRegexSet<T>.CreateDisjunction(builderT, TransformElems(builderT, predicateTransformer));
            else
                return SymbolicRegexSet<T>.CreateConjunction(builderT, TransformElems(builderT, predicateTransformer));
        }

        internal SymbolicRegexNode<S> GetTheElement()
        {
            var en = this.GetEnumerator();
            en.MoveNext();
            var elem = en.Current;
            en.Dispose();
            return elem;
        }

        internal SymbolicRegexSet<S> Reverse()
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, ReverseElems());
            else
                return CreateConjunction(builder, ReverseElems());
        }

        IEnumerable<SymbolicRegexNode<S>> ReverseElems()
        {
            foreach (var elem in this)
                yield return elem.Reverse();
        }

        internal bool StartsWithLoop(int upperBoundLowestValue)
        {
            bool res = false;
            var e = this.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.StartsWithLoop(upperBoundLowestValue))
                {
                    res = true;
                    break;
                }
            }
            e.Dispose();
            return res;
        }

        internal SymbolicRegexSet<S> Simplify()
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, SimplifyElems());
            else
                return CreateConjunction(builder, SimplifyElems());
        }

        IEnumerable<SymbolicRegexNode<S>> SimplifyElems()
        {
            foreach (var elem in this)
                yield return elem.Simplify();
        }

        internal SymbolicRegexSet<S> DecrementBoundedLoopCount(bool makeZero = false)
        {
            if (kind == SymbolicRegexSetKind.Disjunction)
                return CreateDisjunction(builder, DecrementBoundedLoopCountElems(makeZero));
            else
                return CreateConjunction(builder, DecrementBoundedLoopCountElems(makeZero));
        }

        IEnumerable<SymbolicRegexNode<S>> DecrementBoundedLoopCountElems(bool makeZero = false)
        {
            foreach (var elem in this)
                yield return elem.DecrementBoundedLoopCount(makeZero);
        }

        internal bool ContainsAnchors() => this.Any(elem => elem.containsAnchors);

        int enabledBoundedLoopCount = -1;
        internal int EnabledBoundedLoopCount
        {
            get
            {
                if (enabledBoundedLoopCount == -1)
                {
                    int res = 0;
                    var en = this.GetEnumerator();
                    while (en.MoveNext())
                    {
                        res += en.Current.EnabledBoundedLoopCount;
                    }
                    en.Dispose();
                    enabledBoundedLoopCount = res;
                }
                return enabledBoundedLoopCount;
            }
        }

        public IEnumerator<SymbolicRegexNode<S>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        internal string Serialize()
        {
            var list = new List<SymbolicRegexNode<S>>(this);
            var arr = list.ToArray();
            var ser = Array.ConvertAll(arr, x => x.Serialize());
            var str = new List<string>(ser);
            str.Sort();
            return string.Join(",", str);
        }

        internal void Serialize(StringBuilder sb)
        {
            sb.Append(Serialize());
        }

        internal int GetFixedLength()
        {
            if (loops.Count > 0)
                return -1;

            int length = -1;
            foreach (var node in this.set)
            {
                var node_length = node.GetFixedLength();
                if (node_length == -1)
                    return -1;
                else if (length == -1)
                    length = node_length;
                else if (length != node_length)
                    return -1;
            }
            return length;
        }

        /// <summary>
        /// Enumerates all symbolic regexes in the set
        /// </summary>
        public class Enumerator : IEnumerator<SymbolicRegexNode<S>>
        {
            SymbolicRegexSet<S> set;
            bool set_next;
            HashSet<SymbolicRegexNode<S>>.Enumerator set_en;
            bool loops_next;
            Dictionary<Tuple<SymbolicRegexNode<S>, SymbolicRegexNode<S>>, int>.Enumerator loops_en;
            SymbolicRegexNode<S> current;

            internal Enumerator(SymbolicRegexSet<S> symbolicRegexSet)
            {
                this.set = symbolicRegexSet;
                set_en = symbolicRegexSet.set.GetEnumerator();
                loops_en = symbolicRegexSet.loops.GetEnumerator();
                set_next = true;
                loops_next = true;
                current = null;
            }

            public SymbolicRegexNode<S> Current => current;

            object IEnumerator.Current =>  current;

            public void Dispose()
            {
                set_en.Dispose();
                loops_en.Dispose();
            }

            public bool MoveNext()
            {
                if (set_next)
                {
                    set_next = set_en.MoveNext();
                    if (set_next)
                    {
                        current = set_en.Current;
                        return true;
                    }
                    else
                    {
                        loops_next = loops_en.MoveNext();
                        if (loops_next)
                        {
                            var body = loops_en.Current.Key.Item1;
                            var rest = loops_en.Current.Key.Item2;
                            var upper = loops_en.Current.Value;
                            //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                            //TBD:lazy
                            current = set.builder.MkConcat(set.builder.MkLoop(body, false, 0, upper), rest);
                            return true;
                        }
                        else
                        {
                            current = null;
                            return false;
                        }
                    }
                }
                else if (loops_next)
                {
                    loops_next = loops_en.MoveNext();
                    if (loops_next)
                    {
                        var body = loops_en.Current.Key.Item1;
                        var rest = loops_en.Current.Key.Item2;
                        var upper = loops_en.Current.Value;
                        //recreate the symbolic regex from (body,rest)->k to body{0,k}rest
                        current = set.builder.MkConcat(set.builder.MkLoop(body, false, 0, upper), rest);
                        return true;
                    }
                    else
                    {
                        current = null;
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}
