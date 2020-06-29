using System;
using System.Collections.Generic;

namespace Ldartools.Common.Util
{
    public class DelegateEqualityComparer<TType> : IEqualityComparer<TType>
    {
        private readonly Func<TType, object> _selector;

        public DelegateEqualityComparer(Func<TType, object> selector)
        {
            _selector = selector;
        }

        public bool Equals(TType x, TType y)
        {
            return Equals(_selector(x), _selector(y));
        }

        public int GetHashCode(TType obj)
        {
            return _selector(obj).GetHashCode();
        }
    }
}
