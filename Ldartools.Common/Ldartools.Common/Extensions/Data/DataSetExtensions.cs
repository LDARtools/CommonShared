using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Ldartools.Common.Extensions.Data
{
    public static class DataSetExtensions
    {
        public static IEnumerable<DataTable> AsEnumerable(this DataSet set)
        {
            return set.Tables.AsEnumerable();
        }

        public static IEnumerable<DataTable> AsEnumerable(this DataTableCollection tables)
        {
            for (var i = 0; i < tables.Count; i++)
            {
                yield return tables[i];
            }
        }

        public static DataTable[] ToArray(this DataTableCollection tables)
        {
            return tables.AsEnumerable().ToArray();
        }

        public static DataTable[] ToArray(this DataSet set)
        {
            return set.AsEnumerable().ToArray();
        }
    }
}
