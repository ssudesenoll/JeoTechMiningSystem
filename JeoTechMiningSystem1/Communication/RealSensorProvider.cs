using JeoTechMiningSystem1.Models;
using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace JeoTechMiningSystem1.Communication
{
    public class RealSensorProvider : ISensorDataProvider
    {
        private SerialPort _serialPort;
        private List<SensorData> _latestData;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public RealSensorProvider(string portName)
        {
            _latestData = new List<SensorData>();
            try
            {
                _serialPort = new SerialPort(portName, 9600);
                _serialPort.DataReceived += SerialPort_DataReceived;
            }
            catch { /* Handle port init error gracefully */ }
        }

        public void Connect()
        {
            try
            {
                if (_serialPort != null && !_serialPort.IsOpen)
                    _serialPort.Open();
            }
            catch { /* Ignore failure for prototype safety */ }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serialPort.ReadLine();
                // Expected format: "IoT-01,350.5,45.2,15.0"
                string[] parts = line.Split(',');
                if (parts.Length == 4)
                {
                    var data = new SensorData
                    {
                        ModuleId = parts[0],
                        GasValue = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                        Temperature = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                        SmokeValue = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                        Timestamp = DateTime.Now
                    };

                    lock (_latestData)
                    {
                        var existing = _latestData.Find(x => x.ModuleId == data.ModuleId);
                        if (existing != null) _latestData.Remove(existing);
                        _latestData.Add(data);
                    }
                }
            }
            catch { /* Keep alive */ }
        }

        public List<SensorData> GetSensorData()
        {
            lock (_latestData)
            {
                return new List<SensorData>(_latestData);
            }
        }
    }
}
