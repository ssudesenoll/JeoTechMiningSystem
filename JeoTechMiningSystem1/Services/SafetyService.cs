using System.Collections.Generic;
using JeoTechMiningSystem1.Models;
using JeoTechMiningSystem1.Algorithms;

namespace JeoTechMiningSystem1.Services
{
    public class SafetyService
    {
        // Thresholds based on typical workplace safety rules
        private const double GasThreshold = 300.0;
        private const double TempThreshold = 45.0;
        private const double SmokeThreshold = 100.0;

        public void EvaluateSensorData(List<SensorData> sensorDataList, MineGraph graph)
        {
            foreach (var data in sensorDataList)
            {
                bool isDangerous = false;

                data.GasDanger = data.GasValue >= GasThreshold;
                data.TemperatureDanger = data.Temperature >= TempThreshold;
                data.SmokeDanger = data.SmokeValue >= SmokeThreshold;

                if (data.GasDanger || data.TemperatureDanger || data.SmokeDanger)
                {
                    data.Status = SensorStatus.Critical;
                    isDangerous = true;
                }
                else if (data.GasValue >= GasThreshold * 0.8 || data.Temperature >= TempThreshold * 0.8)
                {
                    data.Status = SensorStatus.Warning;
                }
                else
                {
                    data.Status = SensorStatus.Normal;
                }

                if (graph.Nodes.ContainsKey(data.ModuleId))
                {
                    graph.Nodes[data.ModuleId].IsDangerous = isDangerous;
                }
            }
        }
    }
}
