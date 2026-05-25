namespace simSharpSimulation
{
    internal sealed class ArztKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_AERZTE { get; internal set; } = 2;
        public static double MITTLERE_BEHANDLUNGSDAUER { get; internal set; } = 6.6;

        public override int Anzahl => ANZAHL_AERZTE;
        public override double MittlereServicezeit => MITTLERE_BEHANDLUNGSDAUER;
        public override string Beschreibung => "Ärzte in der Klinik";
    }
}
