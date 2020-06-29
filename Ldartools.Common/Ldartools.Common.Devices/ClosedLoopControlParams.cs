namespace Ldartools.Common.Devices
{
    public class ClosedLoopControlParams
    {
        public double? Target { get; set; }
        public int P { get; set; }
        public int I { get; set; }
        public int D { get; set; }
        public double ZO { get; set; }
        public int FF { get; set; }
    }
}