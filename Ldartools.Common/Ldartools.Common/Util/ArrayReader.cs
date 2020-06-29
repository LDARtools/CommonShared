namespace Ldartools.Common.Util
{
    public class ArrayReader<TType>
    {
        private readonly TType[] _array;
        public int Index { get; private set; } = -1;

        public ArrayReader(TType[] array)
        {
            _array = array;
        }

        public bool Read()
        {
            if (HasNextValue)
            {
                Index++;
                return true;
            }

            return false;
        }

        public TType Next()
        {
            return _array[++Index];
        }

        public bool HasNextValue => _array.Length > Index + 1;
        public TType NextValue => _array[Index + 1];
        public TType Value => _array[Index];
    }
}