namespace Ldartools.Common.Util
{
    public class PropertyPathReader : ArrayReader<string>
    {
        public PropertyPathReader(string path) : base(path.Split('.'))
        {
        }
    }
}