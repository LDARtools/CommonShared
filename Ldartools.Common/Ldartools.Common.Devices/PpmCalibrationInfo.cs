using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ldartools.Common.Devices
{
    public class PpmCalibrationInfo
    {
        public int Index { get; set; }
        public float Ppm { get; set; }
        public float H2Pressure { get; set; }
        public int FidCurrent { get; set; }
        public bool IsValid { get; set; }
        public string Timestamp { get; set; }
        public bool IsFactoryCal { get; set; }
    }
}
