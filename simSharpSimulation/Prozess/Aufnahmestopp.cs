using System;

namespace simSharpSimulation
{
    internal static class Aufnahmestopp
    {
        internal static double BerechneZeitpunkt() => Math.Max(0.0,
            SimulationKonfiguration.SIMULATIONSDAUER -
            SimulationKonfiguration.PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN);

        internal static int BerechneArztkapazitaet(double minuten)
        {
            double mittlereBehandlungsdauer = Math.Max(0.1, ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER);
            int kapazitaet = (int)Math.Floor((minuten * ArztKonfiguration.ANZAHL_AERZTE) / mittlereBehandlungsdauer);
            return Math.Max(0, kapazitaet);
        }
    }
}
