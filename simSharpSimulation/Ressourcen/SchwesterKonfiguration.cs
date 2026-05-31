namespace simSharpSimulation
{
    internal sealed class SchwesterKonfiguration : PersonenKonfiguration
    {
        public static int ANZAHL_SCHWESTERN { get; internal set; } = 3;

        public override int Anzahl => ANZAHL_SCHWESTERN;
        public override double MittlereServicezeit => 0.0;
        public override string Beschreibung => "Schwestern in der Klinik";
    }
}
