using System;

namespace JeoTechMiningSystem1.Models
{
    public class SensorData
    {
        public string ModuleId { get; set; }
        public double GasValue { get; set; }
        public double Temperature { get; set; }
        public double SmokeValue { get; set; }
        public bool GasDanger { get; set; }
        public bool TemperatureDanger { get; set; }
        public bool SmokeDanger { get; set; }
        public bool IsConnected { get; set; }
        public DateTime Timestamp { get; set; }
        public SensorStatus Status { get; set; }
    }
}