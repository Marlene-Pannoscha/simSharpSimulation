namespace simSharpSimulation
{
    /// <summary>
    /// Zentrale Konfiguration für die Klinik-Simulation.
    /// Vorteil: Alle Parameter sind an einer Stelle wartbar.
    /// </summary>
    internal static class SimulationKonfiguration
    {
        public const int RANDOM_SEED = 42; // gleiche Zufallswerte bei jedem Lauf.
        public const double MITTLERE_BEHANDLUNGSZEIT = 5.0; // durchschnittliche Dauer, wie lange ein Patient beim Arzt ist.
        public const double SIMULATIONSDAUER = 480.0; // 8 Stunden Simulation für stabilere Statistik
        public const int ANZAHL_AERZTE = 3;

        // Parameter für die Verteilung der Patientenankünfte.
        public const int ANZAHL_PATIENTEN_TAG = 100; // Wie viele Patienten erwarten wir insgesamt heute?
        public const double ERWARTUNGSWERT = 180.0; // Wann ist am meisten los?
        public const double STANDARDABWEICHUNG = 80.0; // Wie breit ist die Glockenkurve?
    }
}
