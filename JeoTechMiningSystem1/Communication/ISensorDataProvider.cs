using JeoTechMiningSystem1.Models;
using System.Collections.Generic;

namespace JeoTechMiningSystem1.Communication
{
    public interface ISensorDataProvider
    {
        List<SensorData> GetSensorData();
        void Connect();
        void Disconnect();
        bool IsConnected { get; }
    }
}
