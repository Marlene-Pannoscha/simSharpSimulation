namespace simSharpSimulation
{
    internal sealed class SchwesterKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_SCHWESTERN { get; internal set; } = 3;
        public static double MITTLERE_BEHANDLUNGSDAUER { get; internal set; } = 4.6;

        public override int Anzahl => ANZAHL_SCHWESTERN;
        public override double MittlereServicezeit => MITTLERE_BEHANDLUNGSDAUER;
        public override string Beschreibung => "Schwestern in der Klinik";
    }
}
