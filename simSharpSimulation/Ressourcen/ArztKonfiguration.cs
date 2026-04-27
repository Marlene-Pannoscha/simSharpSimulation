namespace simSharpSimulation
{
    internal sealed class ArztKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_AERZTE { get; internal set; } = 2;
        public static double MITTLERE_BEHANDLUNGSZEIT { get; internal set; } = 7.0; // durchschnittliche Behandlungszeit beim Arzt

        public override int Anzahl => ANZAHL_AERZTE;
        public override double MittlereServicezeit => MITTLERE_BEHANDLUNGSZEIT;
        public override string Beschreibung => "Ärzte in der Klinik";
    }
}
