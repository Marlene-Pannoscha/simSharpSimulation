namespace simSharpSimulation
{
    internal sealed class SchwesterKonfiguration : PersonenKonfiguration
    {
        public const int ANZAHL_SCHWESTERN = 3;
        public const double MITTLERE_SCHWESTER_ZEIT = 5.0; // durchschnittliche Behandlungszeit bei der Schwester

        public override int Anzahl => ANZAHL_SCHWESTERN;
        public override double MittlereServicezeit => MITTLERE_SCHWESTER_ZEIT;
        public override string Beschreibung => "Schwestern in der Klinik";
    }
}
