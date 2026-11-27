using System;

namespace simSharpSimulation
{
    internal sealed partial class PatientenTagesablauf
    {
        private readonly Random rnd;
        private readonly SimulationsDaten daten;
        internal Aufnahmeprognose Aufnahme { get; }
        private readonly PrognoseRessourcenStatus rezeptionStatus;
        private readonly PrognoseRessourcenStatus schwesterStatus;
        private readonly PrognoseRessourcenStatus arztStatus;

        internal PatientenTagesablauf(Random rnd, SimulationsDaten daten, Tagesressourcen ressourcen)
        {
            this.rnd = rnd;
            this.daten = daten;
            Aufnahme = new Aufnahmeprognose(daten);
            rezeptionStatus = ressourcen.RezeptionStatus;
            schwesterStatus = ressourcen.SchwesterStatus;
            arztStatus = ressourcen.ArztStatus;
        }
    }
}
