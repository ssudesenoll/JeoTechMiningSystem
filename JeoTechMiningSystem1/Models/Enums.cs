namespace JeoTechMiningSystem1.Models
{
    public enum SensorStatus
    {
        Normal,
        Warning,
        Critical
    }

    public enum NodeType
    {
        Junction,
        IoTModule,
        MainExit,
        AlternativeExit,
        Shelter
    }
}
