namespace simSharpSimulation
{
    internal sealed class SchwesterKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_SCHWESTERN { get; internal set; } = 3;
        public static double MITTLERE_SCHWESTER_ZEIT { get; internal set; } = 5.0; // durchschnittliche Behandlungszeit bei der Schwester

        public override int Anzahl => ANZAHL_SCHWESTERN;
        public override double MittlereServicezeit => MITTLERE_SCHWESTER_ZEIT;
        public override string Beschreibung => "Schwestern in der Klinik";
    }
}
