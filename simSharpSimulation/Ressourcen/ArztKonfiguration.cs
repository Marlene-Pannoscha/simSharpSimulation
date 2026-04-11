namespace simSharpSimulation
{
    internal sealed class ArztKonfiguration : PersonenKonfiguration
    {
        public const int ANZAHL_AERZTE = 2;
        public const double MITTLERE_BEHANDLUNGSZEIT = 7.0; // durchschnittliche Behandlungszeit beim Arzt

        public override int Anzahl => ANZAHL_AERZTE;
        public override double MittlereServicezeit => MITTLERE_BEHANDLUNGSZEIT;
        public override string Beschreibung => "Ärzte in der Klinik";
    }
}
