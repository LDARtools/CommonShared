using System;
using System.Collections.Generic;
using System.Linq;
using CG.Commons.Extensions;

namespace Ldartools.Common.Util
{
    public abstract class PickListOrderComparer : IComparer<string>
    {
        private static readonly CG.Commons.Util.NaturalComparer Comparer = new CG.Commons.Util.NaturalComparer();

        protected abstract List<string> TopItems { get; }

        private bool IsTopItem(string s)
        {
            return TopItems.Any(i => i.Equals(s, StringComparison.InvariantCultureIgnoreCase));
        }

        private int Pos(string s)
        {
            return TopItems.IndexOf(i => i.Equals(s, StringComparison.InvariantCultureIgnoreCase));
        }

        public int Compare(string x, string y)
        {
            if (IsTopItem(x) && !IsTopItem(y))
            {
                return -1;
            }
            if (IsTopItem(x) && IsTopItem(y))
            {
                return Pos(x).CompareTo(Pos(y));
            }
            if (!IsTopItem(x) && IsTopItem(y))
            {
                return 1;
            }
            return Comparer.Compare(x, y);
        }
    }
}
