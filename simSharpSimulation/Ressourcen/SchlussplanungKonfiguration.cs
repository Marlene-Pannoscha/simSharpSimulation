namespace simSharpSimulation
{
    internal static class SchlussplanungKonfiguration
    {
        public static bool AKTIVIERT { get; internal set; } = true;
        public static double PROGNOSEFENSTER_MINUTEN_VOR_SCHLUSS { get; internal set; } = 60.0;
        public static double SICHERHEITSFAKTOR { get; internal set; } = 1.15;
        public static int VORMITTAGS_TERMIN_STUNDE { get; internal set; } = 9;
        public static int VORMITTAGS_TERMIN_MINUTE { get; internal set; } = 0;
    }
}
