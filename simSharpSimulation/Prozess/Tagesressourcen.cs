using SimSharp;

namespace simSharpSimulation
{
    internal sealed class Tagesressourcen
    {
        internal Tagesressourcen(Simulation env, int aerzte, int schwestern, int rezeptionisten,
            int arztzimmer, int schwesterzimmer)
        {
            Aerzte = new BeweglicherArztPool(env, aerzte);
            Schwestern = new BeweglicherSchwesterPool(env, schwestern);
            Rezeption = new Resource(env, capacity: rezeptionisten);
            Arztzimmer = new Resource(env, capacity: arztzimmer);
            Schwesterzimmer = new Resource(env, capacity: schwesterzimmer);
            RezeptionStatus = new PrognoseRessourcenStatus(rezeptionisten);
            SchwesterStatus = new PrognoseRessourcenStatus(schwestern);
            ArztStatus = new PrognoseRessourcenStatus(aerzte);
        }

        internal BeweglicherArztPool Aerzte { get; }
        internal BeweglicherSchwesterPool Schwestern { get; }
        internal Resource Rezeption { get; }
        internal Resource Arztzimmer { get; }
        internal Resource Schwesterzimmer { get; }
        internal PrognoseRessourcenStatus RezeptionStatus { get; }
        internal PrognoseRessourcenStatus SchwesterStatus { get; }
        internal PrognoseRessourcenStatus ArztStatus { get; }
    }
}
