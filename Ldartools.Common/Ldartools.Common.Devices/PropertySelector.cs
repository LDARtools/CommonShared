using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Devices
{
    public class PropertySelector
    {
        //phx42 constants
        public static readonly string Current = "Current";
        public static readonly string InternalTemp = "InternalTemp";
        public static readonly string ExternalTemp = "ExternalTemp";
        public static readonly string HPH2 = "HPH2";
        public static readonly string LPH2 = "LPH2";
        public static readonly string SamplePressure = "SamplePressure";
        public static readonly string SamplePpl = "SamplePPL";
        public static readonly string CombustionPpl = "CombustionPPL";
        public static readonly string CombustionPressure = "CombustionPressure";
        public static readonly string PicoAamps = "PicoAmps";
        public static readonly string IsIgnited = "IsIgnited";
        public static readonly string PPM = "PPM";
        public static readonly string Timestamp = "Timestamp";
        public static readonly string BatteryCharge = "BatteryCharge";
        public static readonly string BatteryStatus = "BatteryStatus";
        public static readonly string PPMAverage = "PPMAverage";

        //phx21 constants
        public static readonly string BatteryVoltage = "BatteryVoltage";
        public static readonly string PumpPower = "PumpPower";
        public static Dictionary<string, string> phx42Translation = null;

        public static readonly Dictionary<string, string> Phx21Translation = new Dictionary<string, string>
        {
            {Current, "SystemCurrent"}
            , {InternalTemp, "ThermoCouple"}
            , {ExternalTemp, "ChamberOuterTemp"}
            , {HPH2, "TankPressure"}
            , {LPH2, "AirPressure"}
            , {SamplePpl, "PumpPower"}
            , {CombustionPpl, string.Empty}
            , {CombustionPressure, string.Empty}
            , {PicoAamps, "PicoAmps"}
            , {PPM, "Ppm"}
            , {BatteryCharge, "BatteryVoltage"}
            , {BatteryStatus, string.Empty}
            , {PPMAverage, "LongAveragePpm"}
        };

        public static Dictionary<string, string> Phx42Translation
        {
            get
            {
                if (phx42Translation != null)
                    return phx42Translation;

                phx42Translation = new Dictionary<string, string>();
                
                foreach (var kv in Phx21Translation)
                {
                    phx42Translation[kv.Value] = kv.Key;
                }

                return phx42Translation;
            }
        }

        public static string GetPropertyName(string phxName, string name)
        {
            if (phxName.ToLower().StartsWith("phx42"))
            {
                if (Phx42Translation.ContainsKey(name)) return Phx42Translation[name];

                return name;
            }

            if (Phx21Translation.ContainsKey(name)) return Phx21Translation[name];

            return name;
        }
    }
}
