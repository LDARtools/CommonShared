using System;
using System.Collections.Generic;

namespace Ldartools.Common.Util.Comparers
{
    public class CaseInsensitiveComparer : IEqualityComparer<string>
    {
        #region Implementation of IEqualityComparer<in string>

        public bool Equals(string x, string y)
        {
            if (x == null && y == null) return true;
            if (x != null && y == null) return false;
            return x != null && x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            return obj.ToLowerInvariant().GetHashCode();
        }

        #endregion
    }
}
