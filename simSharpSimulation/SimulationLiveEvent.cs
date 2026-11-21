namespace simSharpSimulation
{
    public record SimulationLiveEvent(
        double Zeit,
        string EventTyp,
        int PatientId,
        string VonZustand,
        string ZuZustand,
        int? ArztId,
        int? SchwesterId,
        string? PersonalId = null,
        string? ArbeitsplatzId = null,
        string? VonRolle = null,
        string? ZuRolle = null,
        double? BewegungsdauerMinuten = null
    );
}