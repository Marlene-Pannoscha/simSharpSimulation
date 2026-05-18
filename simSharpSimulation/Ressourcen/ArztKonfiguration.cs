namespace simSharpSimulation
{
    internal sealed class ArztKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_AERZTE { get; internal set; } = 2;

        public override int Anzahl => ANZAHL_AERZTE;
        public override double MittlereServicezeit => 0.0;
        public override string Beschreibung => "Ärzte in der Klinik";
    }
}
