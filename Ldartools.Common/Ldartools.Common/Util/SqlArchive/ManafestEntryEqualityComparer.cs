using System.Collections.Generic;

namespace Ldartools.Common.Util.SqlArchive
{
    public class ManafestEntryEqualityComparer : IEqualityComparer<ManafestEntry>
    {
        public bool Equals(ManafestEntry x, ManafestEntry y)
        {
            if (x == null && y == null) return true;
            return x != null && y != null && x.ResultSetName == y.ResultSetName;
        }

        public int GetHashCode(ManafestEntry obj)
        {
            return obj.ResultSetName.GetHashCode();
        }
    }
}
