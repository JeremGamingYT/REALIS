namespace REALIS.TrafficAI
{
    public enum CheckpointType
    {
        Random,
        Violation,
        Scheduled
    }
    
    public enum ViolationType
    {
        Speeding,
        RedLightViolation,
        RecklessDriving,
        WrongWay,
        IllegalParking
    }
    
    public enum ViolationSeverity
    {
        Minor,
        Major,
        Severe
    }
} 