namespace simSharpSimulation
{
    public record SimulationLiveEvent(
        double Zeit,
        string EventTyp,
        int PatientId,
        string VonZustand,
        string ZuZustand,
        int? ArztId,
        int? SchwesterId
    );
}