namespace simSharpSimulation
{
    internal static class SimulationKonfiguration
    {
        public const int RANDOM_SEED = 42;
        public const double SIMULATIONSDAUER = 480.0; // 8 Stunden in Minuten

        // Standardwege innerhalb der Klinik und zu/von der Entlassung.
        public const int BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN = 5;
        public const int BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN = 10;
        public const int BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN = 15;
        public const int BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN = 5;
    }
}