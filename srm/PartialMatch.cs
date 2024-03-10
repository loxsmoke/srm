using System.Diagnostics.Tracing;

namespace Microsoft.SRM
{
    public struct PartialMatch
    {
        public int Index { get; private set; }
        public int Length { get; private set; }
        public bool IsFullMatch { get; private set; }

        public PartialMatch(int index, int length, bool fullMatch)
        {
            Index = index;
            Length = length;
            IsFullMatch = fullMatch;
        }

        public static bool operator ==(PartialMatch left, PartialMatch right)
            => left.Index == right.Index && left.Length == right.Length && left.IsFullMatch == right.IsFullMatch;

        public static bool operator!=(PartialMatch left, PartialMatch right) => !(left == right);

        public override bool Equals(object obj) => obj is PartialMatch other && this == other;

        public override int GetHashCode() => (Index, Length).GetHashCode();

        public override string ToString()
        {
            return $"PartialMatch({Index},{Length},{IsFullMatch})";
        }
    }
}