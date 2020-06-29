public class AutoIgnitionParameters
{
    public double SampleSetpoint { get; set; }
    public double CombustionSetpoint { get; set; }
    public double LPH2Setpoint { get; set; }
    public double PressureStableTimeout { get; set; }
    public double SampleStablePressureTol { get; set; }
    public double CombustionStablePressureTol { get; set; }
    public double LPH2StableTol { get; set; }
    public double GlowPlugPowerLevel { get; set; }
    public double CombustionOffTime { get; set; }
    public double GlowPlugDuration { get; set; }
    public double IgnitionTimeout { get; set; }
    public double MinTempRise { get; set; }
    public double CombustionSlope { get; set; }
}