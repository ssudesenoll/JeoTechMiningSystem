using JeoTechMiningSystem1.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JeoTechMiningSystem1.Communication
{
    public class SimulationSensorProvider : ISensorDataProvider
    {
        private List<SensorData> _mockData;
        public bool IsConnected { get; private set; } = true;

        public SimulationSensorProvider()
        {
            Reset();
        }

        public void Connect() { IsConnected = true; }
        public void Disconnect() { IsConnected = false; }

        public void Reset()
        {
            _mockData = new List<SensorData>
            {
                new SensorData { ModuleId = "IoT-01", GasValue = 40, Temperature = 22, SmokeValue = 10, Timestamp = DateTime.Now, Status = SensorStatus.Normal },
                new SensorData { ModuleId = "IoT-02", GasValue = 42, Temperature = 23, SmokeValue = 12, Timestamp = DateTime.Now, Status = SensorStatus.Normal },
                new SensorData { ModuleId = "IoT-03", GasValue = 38, Temperature = 21, SmokeValue = 9, Timestamp = DateTime.Now, Status = SensorStatus.Normal },
                new SensorData { ModuleId = "IoT-04", GasValue = 45, Temperature = 24, SmokeValue = 11, Timestamp = DateTime.Now, Status = SensorStatus.Normal }
            };
        }

        public void SimulateFire(string moduleId)
        {
            var node = _mockData.FirstOrDefault(x => x.ModuleId == moduleId);
            if (node != null)
            {
                node.GasValue = 450;
                node.Temperature = 80;
                node.SmokeValue = 200;
            }
        }

        public void SimulateWarning(string moduleId)
        {
            var node = _mockData.FirstOrDefault(x => x.ModuleId == moduleId);
            if (node != null)
            {
                node.GasValue = 250;
                node.Temperature = 40;
            }
        }

        public List<SensorData> GetSensorData()
        {
            foreach (var d in _mockData) d.Timestamp = DateTime.Now;
            return _mockData;
        }
    }
}
