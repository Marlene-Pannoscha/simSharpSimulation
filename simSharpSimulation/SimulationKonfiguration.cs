namespace simSharpSimulation
{
    internal static class SimulationKonfiguration
    {
        public static int RANDOM_SEED { get; internal set; } = 42;
        public static double SIMULATIONSDAUER { get; internal set; } = 480.0; // 8 Stunden in Minuten
        public static double PROGNOSE_PRUEFUNG_VOR_SCHLIESSUNG_MINUTEN { get; internal set; } = 60.0;

        // Standardwege innerhalb der Klinik und zu/von der Entlassung.
        public static int BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN { get; internal set; } = 5;
        public static int BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN { get; internal set; } = 10;
        public static int BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN { get; internal set; } = 15;
        public static int BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN { get; internal set; } = 5;
    }
}
