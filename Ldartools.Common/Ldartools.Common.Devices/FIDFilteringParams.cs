namespace Ldartools.Common.Devices
{
    public class FIDFilteringParams
    {
        public double IIR { get; set; }
        public int Average { get; set; }
        public int RiseCount { get; set; }
        public int RiseDelta { get; set; }
    }
}